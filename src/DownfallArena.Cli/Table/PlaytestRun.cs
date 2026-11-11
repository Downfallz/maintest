using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A playtest session as it is written down: the bot run's directory exactly — <c>manifest.json</c>,
/// <c>steps.jsonl</c>, <c>episodes.jsonl</c> and one trace — plus the two files only a human session needs
/// (ADR 0054). Nothing about the format is new: <see cref="RunRecorder" /> writes the first four, and the
/// other two go through the same port.
/// </summary>
/// <remarks>
/// Two behaviours here are a human session's and not a batch's. The trace is rewritten after every accepted
/// decision, so a table abandoned at Round 9 leaves a readable partial trace rather than nothing; and
/// <c>catalogue.json</c> is written once at the start, because a trace carries Spell <em>ids</em> and a
/// catalogue tuned twenty times since would read them against cards that no longer exist
/// (<c>docs/tabletop/app-roadmap.md</c>, "A recorded session whose content no longer exists"). A bot run needs
/// neither: it is never interrupted by dinner, and it is regenerated from a seed and a hash.
/// </remarks>
internal sealed class PlaytestRun
{
    public const string NotesFile = "notes.jsonl";
    public const string CatalogueFile = "catalogue.json";

    /// <summary>The table's journal (ADR 0159), started with the run so a session abandoned before its first decision lists it.</summary>
    public const string JournalFile = "decisions.jsonl";

    // How long closing waits for a decision still being written down, and how often it looks. The bound exists
    // so that a thread which is not coming back costs a missing note rather than a table that never closes.
    private static readonly TimeSpan GrowthWait = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan GrowthStep = TimeSpan.FromMilliseconds(5);

    private readonly IArtifactWriter _writer;
    private readonly IArtifactReader _reader;
    private readonly RunRecorder _recorder;
    private readonly MatchTraceRecorder _events;
    private readonly RunStamp _stamp;
    private readonly Lock _seatedGate = new();
    private readonly Dictionary<PlayerSlot, string> _playing = [];
    private readonly TimeProvider _clock;
    private readonly DecisionClock _served;

    // What orders a checkpoint against the closing of the session. Both write the same trace file, and which
    // of them lands last decides whether the session keeps the match it played or a snapshot of half of it.
    private readonly Lock _ordering = new();

    // What the recorder held when the session was closed. It forgets the match at that point, and the page is
    // still watching; null while the match is live, because then the recorder is the truth.
    private IReadOnlyList<TraceEntry>? _kept;

    // Decisions accepted whose notes have not been written yet. The match can end on a tap, and the thread
    // that accepted that tap is still on its way to writing it down while the host is already closing the
    // session -- so closing waits for these, or the finished session it shows is missing its last decision.
    private int _recording;
    private readonly int _seed;

    /// <summary>The record an earlier host left of the table, for a run opened over it (<see cref="Reopen" />).</summary>
    private TableRecord? Reopened
    {
        init
        {
            _record = value ?? _record;
            _revision = value?.Revision ?? 0;
        }
    }

    private PlaytestRun(
        RunPlace place,
        RunRecorder recorder,
        MatchTraceRecorder events,
        RunStamp stamp,
        TimeProvider clock,
        int seed)
    {
        SessionId = place.SessionId;
        Location = place.Location;
        _writer = place.Writer;
        _reader = place.Reader;
        _recorder = recorder;
        _events = events;
        _stamp = stamp;
        _clock = clock;
        _served = new DecisionClock(clock);
        _seed = seed;
    }

    /// <summary>The name of this session's run in its store, and the id every note carries.</summary>
    public string SessionId { get; }

    /// <summary>
    /// Where the session is being written, which its players are told before the first tap: a directory on
    /// this machine, or a blob prefix on Azure (ADR 0080). Said by the store, because only it knows.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Whether the session's files are finished and nothing is still on its way into them: the episodes
    /// written, the final trace written, the manifest rewritten with its counts, and no accepted decision left
    /// to write down. Only then is the directory worth showing anybody.
    /// </summary>
    /// <remarks>
    /// The second half is not the same as the first, and a bounded wait cannot bridge them. Closing waits for
    /// decisions in flight, but that wait has to give up eventually or a thread that is not coming back would
    /// hold a table open — and giving up would otherwise let the page serve a run that says it is finished
    /// while its last note is still arriving. So the wait is what gets the files written, and this is what
    /// says they are all there: a page asked too early is told to come back, which is true, rather than served
    /// a session missing the decision that ended it.
    /// </remarks>
    public bool IsClosed => FilesWritten && Volatile.Read(ref _recording) == 0;

    /// <summary>
    /// Whether the last write has been made: the episodes, the final trace and the manifest with its counts.
    /// Half of <see cref="IsClosed" />; the other half is that nothing is still on its way into them.
    /// </summary>
    private bool FilesWritten { get; set; }

    /// <summary>
    /// Whether checkpoints have stopped, which begins the moment closing does and is a different question from
    /// both of the above. It is what keeps a checkpoint from queueing a write that would land after the final
    /// trace, so it has to be true <em>while</em> the final writes run — reading it as "the session is
    /// readable" is what served a half-written directory once already. Three names because three questions;
    /// folding any two of them together is the bug.
    /// </summary>
    private bool CheckpointsStopped { get; set; }

    /// <summary>
    /// Opens a session under <paramref name="root" />. The rule set is the table's own, so the feature schema
    /// is built from it rather than from the engine default: a dataset whose schema describes a different
    /// rule set than the match played is a dataset that trains on a mislabelled board.
    /// </summary>
    public static PlaytestRun Open(IArtifactStore store, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock) =>
        Open(store, NewId(clock), setup, events, clock);

    /// <summary>
    /// Opens a session under the id the host gave it. The id is minted by the host rather than here because a
    /// session has one whether or not it is recorded: it is what a page, a join code and a pilot token all
    /// name, and a table told <c>--no-record</c> still has to be found by it (ADR 0081).
    /// </summary>
    /// <param name="index">The index the table's record is summarized into (ADR 0161), or none.</param>
    public static PlaytestRun Open(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock, ITableIndex? index = null) =>
        Build(store, id, setup, events, clock, index: index);

    /// <summary>
    /// The recording of a table a host picks up again, when the table is stepped over its journal (ADR 0159).
    /// Nothing is replayed through it and nothing is held back: the match is rebuilt from the journal by the
    /// stepper, the steps are recorded again as it is (<see cref="Witnessed" />), and the notes, the catalogue and
    /// the journal stay as the earlier host left them.
    /// </summary>
    public static PlaytestRun Reopen(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock, TableRecord record, ITableIndex? index = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Build(store, id, setup, events, clock, record, index);
    }

    private static PlaytestRun Build(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock, TableRecord? reopened = null, ITableIndex? index = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        var place = RunPlace.In(store, id);
        var schema = FeatureSchema.Build(setup.Resources, setup.Rules);
        var stamp = RunStamp.Create(EngineVersion.Current, setup.Resources, setup.Rules, schema, setup.Player1Agent, setup.Player2Agent, setup.Seed);

        // One trace, because a session is one match: the limit is what stops the recorder from being asked for
        // a second one it never had.
        var recorder = new RunRecorder(
            place.Writer,
            stamp,
            StepEncoding.For(schema, setup.Resources, setup.Rules),
            clock,
            events,
            traceLimit: 1);

        return new PlaytestRun(place, recorder, events, stamp, clock, setup.Seed) { Reopened = reopened, Index = index };
    }

    /// <summary>
    /// Starts the dataset files and writes what this session is playing. The manifest lands with zero counts,
    /// so a session abandoned before its first decision still says what it was.
    /// </summary>
    public async Task StartAsync(CatalogueView catalogue, CancellationToken cancellationToken = default)
    {
        await _recorder.StartAsync(cancellationToken);
        await _writer.StartJsonLinesAsync(NotesFile, cancellationToken);
        await _writer.StartJsonLinesAsync(JournalFile, cancellationToken);
        await _writer.WriteJsonAsync(CatalogueFile, catalogue, cancellationToken);
    }

    /// <summary>
    /// Whether the match as rebuilt from its journal agrees with the trace the earlier host checkpointed, as far
    /// as both go (ADR 0159). The journal is the table: a checkpoint that runs past it -- a bot's turn applied and
    /// seen by a checkpoint, and never written -- holds entries of a match that is no longer the table's, and
    /// only what the two have in common says whether the record still fits the engine and the content.
    /// </summary>
    public async Task<bool> AgreesWithCheckpointSoFarAsync(MatchId matchId, CancellationToken cancellationToken = default)
    {
        var text = await _reader.ReadTextAsync($"{RunRecorder.TracesDirectory}/{matchId}.json", cancellationToken);
        if (text is null)
        {
            return true;
        }

        using var checkpoint = System.Text.Json.JsonDocument.Parse(text);
        if (!checkpoint.RootElement.TryGetProperty("entries", out var written) || written.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return true;
        }

        var rebuilt = System.Text.Json.JsonSerializer.SerializeToElement(Trace(matchId), ArtifactJson.DocumentOptions);
        return Agree(written, rebuilt, Math.Min(written.GetArrayLength(), rebuilt.GetArrayLength()));
    }

    /// <summary>Whether the first entries of a checkpoint are the first entries of a rebuilt trace.</summary>
    private static bool Agree(System.Text.Json.JsonElement written, System.Text.Json.JsonElement rebuilt, int entries)
    {
        // The boards and where they were, not the events: a player's id is drawn when the seat joins and is
        // on the joining event, and it is nothing the record claims to keep. The boards are the match.
        for (var index = 0; index < entries; index++)
        {
            var entry = written[index];
            var again = rebuilt[index];
            if (!EnergyReceiptAgrees(entry, again))
            {
                return false;
            }

            foreach (var field in new[] { "sequence", "round", "subPhase", "player1", "player2" })
            {
                var had = entry.TryGetProperty(field, out var before);
                var has = again.TryGetProperty(field, out var after);
                if (had != has || (had && !System.Text.Json.JsonElement.DeepEquals(before, after)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Event payloads generally contain identities minted on join. Ordinary energy contains only public facts;
    // compare its payload explicitly so an equal final board cannot conceal a changed grant (ADR 0132).
    private static bool EnergyReceiptAgrees(System.Text.Json.JsonElement before, System.Text.Json.JsonElement after)
    {
        var had = before.TryGetProperty("event", out var oldEvent) && oldEvent.TryGetProperty("kind", out var oldKind) && oldKind.GetString() == "RoundEnergyGranted";
        var has = after.TryGetProperty("event", out var newEvent) && newEvent.TryGetProperty("kind", out var newKind) && newKind.GetString() == "RoundEnergyGranted";
        return had == has && (!had || (oldEvent.TryGetProperty("grants", out var oldGrants) && newEvent.TryGetProperty("grants", out var newGrants) &&
            System.Text.Json.JsonElement.DeepEquals(oldGrants, newGrants) &&
            oldEvent.TryGetProperty("roundId", out var oldRound) && newEvent.TryGetProperty("roundId", out var newRound) &&
            System.Text.Json.JsonElement.DeepEquals(oldRound, newRound)));
    }

    /// <summary>
    /// Writes the table down as it was opened, so a later host can rebuild it (ADR 0091), and again whenever
    /// its seats change (ADR 0115). A record that already says the table ended keeps saying so: a seat claimed
    /// or left in the same moment the match ends must not write it back to open, or the next host would rebuild
    /// a finished match. The writes land in the order they were made, the same queue <see cref="MarkAsync" />
    /// joins, so the last one written is the last one decided.
    /// </summary>
    public Task RecordAsync(TableRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_recordGate)
        {
            // A table can end before it is first written down (two bots are quick): its end, marked then, is
            // what the first record says, or the table would be open in its record and in the index for good.
            var ended = _record is null ? _markedFirst : _record.Status;
            _record = ended is null or TableRecord.Open ? record : record with { Status = ended };
            var snapshot = _record;
            return _recordWritten = WriteRecordAfterAsync(_recordWritten, _ => snapshot, cancellationToken);
        }
    }

    /// <summary>
    /// Marks the record with how the table ended -- finished, or closed without finishing -- so a later host
    /// leaves it alone. Nothing is written when the table was never recorded, and a write that fails is a
    /// line on the console: a table being let go of must not stay for a store that is out.
    /// </summary>
    public async Task MarkAsync(string status, CancellationToken cancellationToken = default)
    {
        Task written;
        lock (_recordGate)
        {
            if (_record is null)
            {
                // Nothing written down yet: the first record says it (RecordAsync).
                _markedFirst = status;
                return;
            }

            if (_record is not { } record || record.Status == status)
            {
                return;
            }

            // Over the record as stored, not this one: a mark changes nothing else, and another process may have
            // changed the seats' keys since this one last wrote (ADR 0162).
            _record = record with { Status = status };
            var fallback = _record;
            written = _recordWritten = WriteRecordAfterAsync(_recordWritten, stored => (stored ?? fallback) with { Status = status }, cancellationToken);
        }

        try
        {
            await written;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException or Azure.RequestFailedException)
        {
            Console.WriteLine($"  Session {SessionId} could not be marked {status}: {failure.Message}");
        }
    }

    /// <summary>
    /// Marks the record as refused under this basis, so a host on the same basis leaves it as it is instead of
    /// replaying it at every start. The status is untouched: a table that could not be rebuilt stays open for the
    /// operator, who closes or deletes it (ADR 0115). A write that fails is a line on the console, and the next host
    /// tries the replay again.
    /// </summary>
    public async Task MarkRefusedAsync(ReplayBasis basis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(basis);
        Task written;
        lock (_recordGate)
        {
            if (_record is not { } record || record.RefusedUnder == basis)
            {
                return;
            }

            _record = record with { RefusedUnder = basis };
            var fallback = _record;
            written = _recordWritten = WriteRecordAfterAsync(_recordWritten, stored => (stored ?? fallback) with { RefusedUnder = basis }, cancellationToken);
        }

        try
        {
            await written;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException or Azure.RequestFailedException)
        {
            Console.WriteLine($"  Session {SessionId} could not be marked as refused: {failure.Message}");
        }
    }

    private TableRecord? _record;
    private string? _markedFirst;
    private readonly Lock _recordGate = new();
    private Task _recordWritten = Task.CompletedTask;

    /// <summary>
    /// One record write after the one before it, whatever became of that one: a failed write is its own caller's.
    /// Written over the record as stored and only if nobody wrote it since it was read (ADR 0159): another host
    /// may hold the same table, and the record keeps what either of them wrote that the other must not undo.
    /// </summary>
    private async Task WriteRecordAfterAsync(Task before, Func<TableRecord?, TableRecord> over, CancellationToken cancellationToken)
    {
        await Task.WhenAny(before);
        var (stored, written) = await WriteOverAsync(_reader, _writer, SessionId, over, cancellationToken);
        var known = Interlocked.Exchange(ref _revision, written.Revision);
        if (known > 0 && stored is { } other && other.Revision > known)
        {
            // Another process wrote the record since this one last did, and the watch had not seen it yet: what this
            // write says of the seats is this process's, and the other's is lost. Rare -- a seat changing hands here
            // in the second an hourly job expires its invitation -- and said, not hidden.
            Console.WriteLine($"  Session {SessionId}: its record was written by another process (revision {other.Revision}) since this one last wrote it ({known}); this write keeps what this process holds.");
        }
        await IndexedAsync(stored, written, cancellationToken);
    }

    // The revision of the record as this process last wrote it, or read it as it reopened the run.
    private long _revision;

    /// <summary>
    /// The table's record, when another process has written it since this one last did (ADR 0162): its revision is
    /// past the one this process wrote. None otherwise, and never while a write of this process's own is on its way,
    /// which would read as one. A process holding the table catches up with it, or lets it go and picks it up again
    /// as the record now says it is.
    /// </summary>
    public async Task<TableRecord?> RewrittenAsync(CancellationToken cancellationToken = default)
    {
        Task pending;
        lock (_recordGate)
        {
            pending = _recordWritten;
        }

        if (!pending.IsCompleted)
        {
            return null;
        }

        var read = await _reader.ReadTaggedAsync(TableRecord.File, cancellationToken);
        return read is not null && StoredSessions.RecordOf(read.Text) is { } stored
            && pending == Volatile.Read(ref _recordWritten)
            && stored.Revision > Interlocked.Read(ref _revision)
            ? stored
            : null;
    }

    /// <summary>
    /// A record written over the one stored, on the condition that nobody wrote it since it was read, read again
    /// and written again when somebody had (ADR 0159). What is written is worked out from what is stored.
    /// </summary>
    private static async Task<(TableRecord? Stored, TableRecord Written)> WriteOverAsync(IArtifactReader reader, IArtifactWriter writer, string sessionId, Func<TableRecord?, TableRecord> over, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var read = await reader.ReadTaggedAsync(TableRecord.File, cancellationToken);
            var stored = read is null ? null : StoredSessions.RecordOf(read.Text);
            var written = over(stored).Over(stored);
            if (await writer.WriteJsonIfAsync(TableRecord.File, written, read?.Tag, cancellationToken) is not null)
            {
                return (stored, written);
            }

            if (attempt == RecordAttempts)
            {
                throw new IOException($"The record of session {sessionId} was written by somebody else at every one of {RecordAttempts} attempts.");
            }
        }
    }

    /// <summary>
    /// Closes a table no host holds, through its record (ADR 0161): marked closed, so no host picks it up again,
    /// and out of the open tables of the index. Nothing for a table whose record is not open, or that has none.
    /// </summary>
    /// <returns>Whether the record was open and is now closed.</returns>
    public static async Task<bool> CloseRecordAsync(IArtifactStore store, string id, ITableIndex index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        var place = RunPlace.In(store, id);
        var open = false;
        var (stored, written) = await WriteOverAsync(place.Reader, place.Writer, id, stored =>
        {
            if (stored is null)
            {
                throw new InvalidDataException($"Session {id} has no table record.");
            }

            open = stored.Status == TableRecord.Open;
            return open ? stored with { Status = TableRecord.Closed } : stored;
        }, cancellationToken);
        await new TableIndexer(index, id).FollowAsync(stored, written, cancellationToken);
        return open;
    }

    private const int RecordAttempts = 5;

    /// <summary>The index that finds this table (ADR 0161), or none on a host that keeps no index.</summary>
    public ITableIndex? Index
    {
        get => _indexer?.Index;
        init => _indexer = value is null ? null : new TableIndexer(value, SessionId);
    }

    private readonly TableIndexer? _indexer;

    /// <summary>The index brought in line with a record just written (<see cref="TableIndexer" />).</summary>
    private Task IndexedAsync(TableRecord? before, TableRecord written, CancellationToken cancellationToken) =>
        _indexer?.FollowAsync(before, written, cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Whoever is about to decide for a seat of a stepped match, recorded as an agent seated there would be: the
    /// same steps, each under the name of whoever the table says holds the seat at that board's round, and the
    /// run stamp growing at the first decision a new occupant makes. Nothing sits between the match and its
    /// seats to record through any more, so the stepper shows every decision to this instead (ADR 0159).
    /// </summary>
    /// <param name="named">Who holds the seat of a board at that board's round, by the name a record calls them.</param>
    public IPlayerAgent Witnessed(MatchId matchId, IPlayerAgent decider, Func<PlayerBoardState, string> named)
    {
        ArgumentNullException.ThrowIfNull(decider);
        ArgumentNullException.ThrowIfNull(named);
        return _recorder.Wrap(matchId, decider, board =>
        {
            var name = named(board);
            Reseated(board.Slot, name, board.RoundNumber);
            return new Decider(decider, name);
        });
    }

    /// <summary>
    /// Lets go of what was recorded of a match about to be rebuilt from its journal: the replay records every
    /// step again and says again which seats changed hands.
    /// </summary>
    public void Forget(MatchId matchId)
    {
        lock (_seatedGate)
        {
            _playing.Clear();
        }

        _recorder.Forget(matchId);
    }

    /// <summary>
    /// Notices that a seat has actually changed hands and grows the run stamp to say so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Here rather than where a swap is asked for, because the two are different events: a swap names a round
    /// the match has not reached, and the match can end before it. A stamp written from the asking would name
    /// a player who never played, in the one axis <c>compare-stamps</c> reads to decide whether two runs are
    /// comparable. This runs at the first decision the new occupant makes, which is the earliest moment the
    /// claim is true.
    /// </para>
    /// <para>
    /// It only ever appends, so a seat that changed twice says so twice, and the stamp reads in the order it
    /// happened. Nothing is written here: the manifest is rewritten when the session closes, and an abandoned
    /// session keeps the opening one, which names exactly the occupants that had played by then.
    /// </para>
    /// </remarks>
    private void Reseated(PlayerSlot slot, string? name, int? round)
    {
        if (name is null)
        {
            return;
        }

        lock (_seatedGate)
        {
            // Seeded from the stamp the manifest was opened with, never from the first name seen. A swap can
            // land before this seat's first decision -- `--handover 1` is exactly that -- and taking the first
            // observation as the baseline would record the new occupant as the original one: the manifest
            // would say the bot played a session every step of which names the person.
            var held = _playing.TryGetValue(slot, out var seen) ? seen : Opened(slot);
            if (held == name)
            {
                _playing[slot] = name;
                return;
            }

            _playing[slot] = name;
            _recorder.Reseated(slot, name, round);
        }
    }

    private string Opened(PlayerSlot slot) => slot == PlayerSlot.Player1 ? _stamp.Player1Agent : _stamp.Player2Agent;

    /// <summary>
    /// Records that a seat has been shown what it is being asked, which is what the next decision's duration
    /// is measured from.
    /// </summary>
    public void Served(PlayerSlot slot, Asking? question) => _served.Served(slot, question);

    /// <summary>
    /// When this seat's current options were served, read before the decision is handed over so a question
    /// asked in its wake cannot take the moment with it.
    /// </summary>
    public DateTimeOffset? ServedAt(PlayerSlot slot, Asking? question) => _served.ServedAt(slot, question);

    /// <summary>This session's clock, so a caller reads the moment of an event where the event happens.</summary>
    public DateTimeOffset Now() => _clock.GetUtcNow();

    /// <summary>
    /// Declares that a decision is being accepted and will be written down. Held from before the decision
    /// reaches the seat until its note is written, so closing the session cannot step over it.
    /// </summary>
    public IDisposable Accepting() => new Acceptance(this);

    /// <summary>
    /// A decision was accepted, timed from <paramref name="servedAt" /> -- the moment the caller read off
    /// <see cref="ServedAt" /> before submitting it. Null means nothing knows when the options reached a
    /// person -- a scripted seat, or a tap that beat the page's own word that the board was up -- and is
    /// recorded as an unknown duration, never as zero.
    /// </summary>
    public Task DecidedAsync(
        MatchId matchId,
        PlayerSlot slot,
        int? round,
        RoundSubPhase? subPhase,
        AcceptedDecision accepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        _served.Answered(slot, accepted.Answered);

        // A tie order is a decision with no step beside it: the recording agent records none, since no encoding
        // of one exists (ADR 0063). A note for it would put every later Decision note beside the wrong step, and
        // the alignment is by order and nothing else (playtest-app.md 5.3).
        if (accepted.Answered?.Kind == PlayerOptionsKind.TieOrder)
        {
            return Task.CompletedTask;
        }

        return NoteAsync(
            PlaytestNote.Decision(Where(matchId, slot, round, subPhase), accepted.ServedAt, accepted.AcceptedAt, accepted.Answered?.Asked),
            cancellationToken);
    }

    /// <summary>
    /// A decision was refused. The clock is left alone: the seat is still being asked the same question, and
    /// the time a player spent being refused is part of how long that question took them.
    /// </summary>
    public Task RefusedAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, DomainError error, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Refused(Where(matchId, slot, round, subPhase), error, _clock), cancellationToken);

    /// <summary>
    /// The pilot asked this seat to change hands at the top of a later round.
    /// </summary>
    /// <remarks>
    /// Of the asking, and dated where the match had got to when the seat took it — not where the swap will
    /// land. The two rounds are both on the note and they answer different questions: this one puts the note
    /// beside the decisions that prompted it, and <c>atRound</c> says where it takes effect. There is no
    /// sub-phase, because a swap is not made at one: it is an operator's action against a round.
    /// </remarks>
    public Task SeatedAsync(MatchId matchId, PlayerSlot slot, int? round, SeatChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        return NoteAsync(PlaytestNote.Seated(Where(matchId, slot, round, subPhase: null), change.From, change.To, change.AtRound, _clock), cancellationToken);
    }

    /// <summary>This seat gave the match up, where the match had got to (ADR 0087).</summary>
    public Task ConcededAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Conceded(Where(matchId, slot, round, subPhase), _clock), cancellationToken);

    /// <summary>A note a player produced with one tap, or typed on the end screen.</summary>
    public Task TypedAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, NoteKind kind, string text, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Typed(Where(matchId, slot, round, subPhase), kind, text, _clock), cancellationToken);

    /// <summary>One decision on its way from accepted to written down.</summary>
    private sealed class Acceptance : IDisposable
    {
        private readonly PlaytestRun _run;
        private bool _done;

        public Acceptance(PlaytestRun run)
        {
            _run = run;
            Interlocked.Increment(ref run._recording);
        }

        public void Dispose()
        {
            if (!_done)
            {
                _done = true;
                Interlocked.Decrement(ref _run._recording);
            }
        }
    }

    /// <summary>Where a note is being taken, which only this session can say, because only it knows its own id.</summary>
    private NotePlace Where(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase) =>
        new(SessionId, matchId, slot, round, subPhase);

    private Task NoteAsync(PlaytestNote note, CancellationToken cancellationToken) =>
        _writer.AppendJsonLinesAsync(NotesFile, [note], cancellationToken);

    /// <summary>
    /// The match's trace, live or as closing kept it: a record whose last line ends the match closes the
    /// session during its own replay, and the recorder forgets the match as it is handed it.
    /// </summary>
    private IReadOnlyList<TraceEntry> Trace(MatchId matchId)
    {
        // The live trace is read first: closing keeps the trace and then hands the match to the recorder,
        // which forgets it, so what was kept is read after, and is the whole trace whenever it is there.
        var live = _events.EntriesOf(matchId);
        return _kept ?? live;
    }

    /// <summary>
    /// The match's entries from <paramref name="since" /> onwards, whether or not the session has been closed.
    /// </summary>
    /// <remarks>
    /// Closing a session hands the finished trace to the recorder, and the recorder then forgets the match --
    /// but the host keeps serving, because two people want to read the end of the match they just played. The
    /// feed would go empty at exactly that moment and never recover, so whatever happened in the last poll's
    /// worth of match, the deciding blow and the outcome among it, would be on nobody's screen. The entries are
    /// kept here as closing takes them, and served from here afterwards.
    /// </remarks>
    public IReadOnlyList<TraceEntry> Entries(MatchId matchId, int since) =>
        _kept is { } final
            ? [.. final.Skip(Math.Clamp(since, 0, final.Count))]
            : _events.EntriesOf(matchId, since);

    /// <param name="upTo">How many of the match's entries are its own, as its journal holds it; every one when none.</param>
    public Task CheckpointAsync(MatchId matchId, int? upTo, CancellationToken cancellationToken = default)
    {
        // Reading the trace and queueing its write happen together, under the same lock the closing takes. The
        // null above is not enough on its own: a checkpoint can read a partial trace while the match is still
        // held, lose its thread before it queues anything, and resume after the finished trace has been
        // written -- and because the writer orders by when a write was queued and not by when its content was
        // read, the stale partial one would land last. Nothing is awaited in here, so the lock is held for a
        // copy and an enqueue.
        lock (_ordering)
        {
            if (CheckpointsStopped)
            {
                return Task.CompletedTask;
            }

            if (_events.Snapshot(matchId, _stamp, _seed, upTo) is not { } trace)
            {
                return Task.CompletedTask;
            }

            // Queued behind the one before, so this process's checkpoints land in the order they were taken.
            var before = _checkpointed;
            return _checkpointed = CheckpointAfterAsync(before, $"{RunRecorder.TracesDirectory}/{matchId}.json", trace, cancellationToken);
        }
    }

    /// <summary>Rewrites the trace as it stands, every entry of it: see the other overload.</summary>
    public Task CheckpointAsync(MatchId matchId, CancellationToken cancellationToken = default) => CheckpointAsync(matchId, upTo: null, cancellationToken);

    // The last checkpoint queued: the next one is written after it.
    private Task _checkpointed = Task.CompletedTask;

    /// <summary>
    /// A checkpoint written after the one before it, and never over a finished trace: another process may hold the
    /// table, close it and write the match's trace with its outcome while this one's checkpoint is on its way
    /// (ADR 0162). It is written only over the trace it read, and read again when somebody wrote it since.
    /// </summary>
    private async Task CheckpointAfterAsync(Task before, string path, MatchTrace trace, CancellationToken cancellationToken)
    {
        await Task.WhenAny(before);
        for (var attempt = 1; attempt <= RecordAttempts; attempt++)
        {
            var read = await _reader.ReadTaggedAsync(path, cancellationToken);
            if (read is not null && Finished(read.Text))
            {
                return;
            }

            if (await _writer.WriteJsonIfAsync(path, trace, read?.Tag, cancellationToken) is not null)
            {
                return;
            }
        }
    }

    /// <summary>Whether a trace as written is a finished match's: its outcome is on it.</summary>
    private static bool Finished(string trace)
    {
        try
        {
            using var document = JsonDocument.Parse(trace);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(property => string.Equals(property.Name, nameof(MatchTrace.Outcome), StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind != JsonValueKind.Null);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Closes the session: the episodes, the final trace, and the manifest with its counts. The board is the
    /// one the match ended on, which is what an episode's return is read from.
    /// </summary>
    /// <returns>
    /// Whether the files were written: not when this run claimed the closing (<see cref="ClaimClosingAsync" />) and
    /// another run took the claim over before one of its writes, which is left to that run.
    /// </returns>
    public async Task<bool> FinishAsync(MatchId matchId, PlayerBoardState player1Board, CancellationToken cancellationToken = default)
    {
        // Checkpoints are stopped before anything final is written, not after. A checkpoint already inside the
        // lock queues its write first and the finished trace lands on top of it; one arriving afterwards finds
        // the session closing and does nothing. The order is the point: the last write to the trace has to be
        // the final one.
        lock (_ordering)
        {
            CheckpointsStopped = true;
        }

        // Decisions already accepted finish being written down first. The match can end on a tap, and the
        // thread that took that tap is still on its way here -- a session closed over it would be shown as
        // finished with its last decision missing from notes.jsonl and present in steps.jsonl. Bounded for the
        // same reason as everything else on this path: a thread that is not coming back must not hold a table
        // open.
        for (var waited = TimeSpan.Zero; Volatile.Read(ref _recording) > 0 && waited < GrowthWait; waited += GrowthStep)
        {
            await Task.Delay(GrowthStep, _clock, cancellationToken);
        }

        // Taken before the recorder is handed the match, because being handed it is what makes it forget.
        _kept = _events.EntriesOf(matchId);

        // Another run may have got as far as writing the steps and the episodes and no further -- it stopped
        // before the record said finished, or could not write that: an earlier host, or another process whose
        // claim on closing ran out (ADR 0162), which this run need not have been opened over. Written into again
        // as they are, a dataset would hold the match twice under a manifest that counts it once, so they are
        // started over: the steps and the episodes are only ever written here, at the close. Each write is made
        // only while this run's claim on closing still stands, renewed before it (ADR 0162): a run that stalled
        // past its claim and was taken over from stops at its next write, and leaves the rest to the other.
        if (!await StillOursAsync(cancellationToken))
        {
            return false;
        }

        await _recorder.StartAsync(cancellationToken);
        if (!await StillOursAsync(cancellationToken))
        {
            return false;
        }

        await _recorder.MatchPlayedAsync(matchId, _seed, player1Board, cancellationToken);
        if (!await StillOursAsync(cancellationToken))
        {
            return false;
        }

        await _recorder.FinishAsync(cancellationToken);
        if (!await StillOursAsync(cancellationToken))
        {
            return false;
        }

        await MarkAsync(TableRecord.Finished, cancellationToken);

        // And only now are the files written. Saying so at the top of this method would let the session page
        // answer while the manifest still says the match played nothing, or while a file it is about to embed
        // is half written. Whether anything is still on its way into them is the other half of IsClosed.
        FilesWritten = true;
        return true;
    }

    /// <summary>Whether this run may write the session's files: it claimed nothing, or its claim still stands.</summary>
    private async Task<bool> StillOursAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _claimed) == 0 || await StillClosingAsync(cancellationToken);

    // Whether this run claimed the closing of the session, whose writes are then made only while the claim stands.
    private int _claimed;

    /// <summary>The claim one process makes on closing the session's files (ADR 0162), beside them.</summary>
    public const string ClosingFile = "closing.json";

    /// <summary>
    /// How long a claim on closing the session stands. Closing takes a few writes; a claim older than this is a
    /// process that stopped between its claim and its last write, and another closes the session in its place.
    /// </summary>
    public static readonly TimeSpan ClosingHeld = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Claims the closing of the session's files for this run, so that one process closes them and tells the seats
    /// how the match ended, however many hold the table (ADR 0162): the worker that took the bot's last turn, the
    /// host whose seat made the last tap, and a host that caught up with either. A claim is a document written only
    /// where there is none, or where the one there is older than <see cref="ClosingHeld" />.
    /// </summary>
    /// <returns>Whether this run is the one to close the session: not when its record already says it finished, or
    /// another run's claim stands.</returns>
    public async Task<bool> ClaimClosingAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= RecordAttempts; attempt++)
        {
            if (await FinishedAsync(cancellationToken))
            {
                return false;
            }

            // Two machines' clocks compared: a claim's two minutes are a few seconds more or less, which only moves
            // how soon a process that stopped is taken over from.
            var now = _clock.GetUtcNow();
            var read = await _reader.ReadTaggedAsync(ClosingFile, cancellationToken);
            if (read is not null && ClosingOf(read.Text) is { } held && held.By != _closer && now - held.At < ClosingHeld)
            {
                return false;
            }

            if (await _writer.WriteJsonIfAsync(ClosingFile, new Closing(_closer, now), read?.Tag, cancellationToken) is not null)
            {
                Volatile.Write(ref _claimed, 1);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether this run's claim on closing the session still stands, renewed for another <see cref="ClosingHeld" />
    /// when it does: written only over the claim it read, so a run whose claim another took over -- it stalled
    /// longer than a claim stands -- finds out before it writes the files or tells anybody, and stops.
    /// </summary>
    public async Task<bool> StillClosingAsync(CancellationToken cancellationToken = default)
    {
        var read = await _reader.ReadTaggedAsync(ClosingFile, cancellationToken);
        return read is not null
            && ClosingOf(read.Text)?.By == _closer
            && await _writer.WriteJsonIfAsync(ClosingFile, new Closing(_closer, _clock.GetUtcNow()), read.Tag, cancellationToken) is not null;
    }

    /// <summary>
    /// Whether another process has closed the session's files, as the record says once they are all written. When
    /// it has, this run takes them as written: the session page is served, and what this run kept of the match to
    /// write is let go of.
    /// </summary>
    public async Task<bool> ClosedElsewhereAsync(MatchId matchId, CancellationToken cancellationToken = default)
    {
        if (!await FinishedAsync(cancellationToken))
        {
            return false;
        }

        lock (_ordering)
        {
            CheckpointsStopped = true;
        }

        _kept = _events.EntriesOf(matchId);
        _recorder.Forget(matchId);
        FilesWritten = true;
        return true;
    }

    private async Task<bool> FinishedAsync(CancellationToken cancellationToken) =>
        await _reader.ReadTextAsync(TableRecord.File, cancellationToken) is { } text && StoredSessions.RecordOf(text)?.Status == TableRecord.Finished;

    private static Closing? ClosingOf(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<Closing>(text, ArtifactJson.DocumentOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Who this run is, to its own claim: a run picked up again by the same process is another run.
    private readonly string _closer = Guid.NewGuid().ToString("N");

    /// <summary>A claim on closing the session: by which run, and when it was made.</summary>
    private sealed record Closing(string By, DateTimeOffset At);

    /// <summary>
    /// Waits for everything on its way into the files to land: every decision being accepted, then a write
    /// queued behind every write before it, which the writers take in turn. What is deleted or read after this
    /// is deleted or read after the last write, not beside it. Bounded like every other wait on a thread that
    /// may not be coming back.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        for (var waited = TimeSpan.Zero; Volatile.Read(ref _recording) > 0 && waited < DrainWait; waited += GrowthStep)
        {
            await Task.Delay(GrowthStep, _clock, cancellationToken);
        }

        // Appending nothing is a write that lands after every write queued before it, in either store.
        await _writer.AppendJsonLinesAsync(NotesFile, Array.Empty<PlaytestNote>(), cancellationToken);
    }

    private static readonly TimeSpan DrainWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The session's files, as the viewer reads them. The trace comes first because the viewer opens on the
    /// first artifact it is handed and the thing two people want the moment they finish is the match they just
    /// played; the dataset files follow. <c>notes.jsonl</c> and <c>catalogue.json</c> are handed over too and
    /// the viewer ignores both today, which is deliberate: it can learn to read them later without the files
    /// having to be invented then.
    /// </summary>
    public Task<IReadOnlyList<(string Name, string Text)>> ArtifactsAsync(CancellationToken cancellationToken = default) =>
        StoredSessions.ArtifactsOf(_reader, cancellationToken);

    /// <summary>
    /// A session id: when it was opened, to the second, so it sorts by when it was played, and four random
    /// bytes so two tables opened in the same second are two ids. It is the run's name in its store, the id
    /// every note carries, and the one thing the tokenless session page is reached by, which is why it is
    /// not a counter. The clock is the injected one, so a test names a session rather than racing one.
    /// </summary>
    public static string NewId(TimeProvider clock) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{clock.GetUtcNow():yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}");
}
