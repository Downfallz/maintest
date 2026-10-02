using System.Globalization;
using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
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

    /// <summary>What a record and a journal name beside the dataset: the two files a rebuild reads (ADR 0091).</summary>
    public static readonly IReadOnlyList<string> RebuildFiles = [TableRecord.File, DecisionJournal.File];

    // How long a checkpoint waits for the decision it follows to reach the trace, and how often it looks. The
    // driver applies one command in microseconds, so this is all but always over on the first look; the bound
    // exists so that a driver which is not coming back costs a stale trace rather than a hanging tap.
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

    private PlaytestRun(
        RunPlace place,
        RunRecorder recorder,
        MatchTraceRecorder events,
        RunStamp stamp,
        TimeProvider clock,
        int seed,
        Rebuild? rebuild)
    {
        Journal = new DecisionJournal(place.Writer, clock, rebuild?.Recorded);
        _record = rebuild?.Record;
        _staged = rebuild?.Staged;
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

    /// <summary>Every decision of the match as it is taken, and what a rebuild replays (ADR 0091).</summary>
    public DecisionJournal Journal { get; }

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
    public static PlaytestRun Open(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock) =>
        Build(store, id, setup, events, clock, rebuilding: null);

    /// <summary>
    /// Opens a session over what an earlier host wrote, to replay it: the journal starts full and the files
    /// are not started over. The dataset files are, because their steps were in the earlier host's memory
    /// and are re-recorded by the replay, but not before <see cref="KeepAsync" />: a replay can be refused,
    /// and a run refused is left as the earlier host wrote it. The notes, the catalogue, the record and the
    /// journal were written as they happened and are kept; the record is the one marked when this table ends.
    /// </summary>
    public static PlaytestRun Resume(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock, TableRecord record, IReadOnlyList<JournalEntry> recorded)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(recorded);
        return Build(store, id, setup, events, clock, (record, recorded));
    }

    private static PlaytestRun Build(IArtifactStore store, string id, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock, (TableRecord Record, IReadOnlyList<JournalEntry> Recorded)? rebuilding)
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
        // a second one it never had. On a rebuild its writes are held back until the replay is kept.
        var staged = rebuilding is null ? null : new StagedArtifactWriter(place.Writer);
        var recorder = new RunRecorder(
            staged ?? place.Writer,
            stamp,
            new ObservationBuilder(schema),
            new ActionEncoder(schema),
            new CandidateTerms(setup.Resources, setup.Rules),
            clock,
            events,
            traceLimit: 1);

        return new PlaytestRun(place, recorder, events, stamp, clock, setup.Seed, rebuilding is { } rebuild ? new Rebuild(rebuild.Record, rebuild.Recorded, staged!) : null);
    }

    /// <summary>What a resumed run is resumed over: the record it will mark, the lines it replays, and the writer that holds the replay's dataset back.</summary>
    private sealed record Rebuild(TableRecord Record, IReadOnlyList<JournalEntry> Recorded, StagedArtifactWriter Staged);

    /// <summary>
    /// Starts the dataset files and writes what this session is playing. The manifest lands with zero counts,
    /// so a session abandoned before its first decision still says what it was.
    /// </summary>
    public async Task StartAsync(CatalogueView catalogue, CancellationToken cancellationToken = default)
    {
        await _recorder.StartAsync(cancellationToken);
        await _writer.StartJsonLinesAsync(NotesFile, cancellationToken);
        await _writer.StartJsonLinesAsync(DecisionJournal.File, cancellationToken);
        await _writer.WriteJsonAsync(CatalogueFile, catalogue, cancellationToken);
    }

    /// <summary>Starts the dataset files over for a replay, and nothing else: see <see cref="Resume" />. Held back until <see cref="KeepAsync" />.</summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default) => _recorder.StartAsync(cancellationToken);

    /// <summary>
    /// The replay reached the end of the record and the rebuilt match agrees with it: the dataset files it
    /// re-recorded land now, in the order they were written, and every write from here on lands as it comes.
    /// Nothing to do for a run opened rather than resumed.
    /// </summary>
    public Task KeepAsync(CancellationToken cancellationToken = default) => _staged?.LetThroughAsync(cancellationToken) ?? Task.CompletedTask;

    private readonly StagedArtifactWriter? _staged;

    /// <summary>
    /// Whether the match as rebuilt agrees with the trace the earlier host checkpointed: every entry it wrote
    /// is an entry of the rebuilt trace, in its place. A record can fit a match structurally and still be
    /// another match -- the same legal moves under another seed, or under content that changed -- and the
    /// boards the checkpoint holds are what says so. True when there is no checkpoint to hold it against.
    /// </summary>
    public async Task<bool> AgreesWithCheckpointAsync(MatchId matchId, CancellationToken cancellationToken = default)
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
        if (written.GetArrayLength() > rebuilt.GetArrayLength())
        {
            return false;
        }

        // The boards and where they were, not the events: a player's id is drawn when the seat joins and is
        // on the joining event, and it is nothing the record claims to keep. The boards are the match.
        var index = 0;
        foreach (var entry in written.EnumerateArray())
        {
            var again = rebuilt[index++];
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

    /// <summary>Writes the table down as it was opened, so a later host can rebuild it (ADR 0091).</summary>
    public Task RecordAsync(TableRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _record = record;
        return _writer.WriteJsonAsync(TableRecord.File, record, cancellationToken);
    }

    /// <summary>
    /// Marks the record with how the table ended -- finished, or closed without finishing -- so a later host
    /// leaves it alone. Nothing is written when the table was never recorded, and a write that fails is a
    /// line on the console: a table being let go of must not stay for a store that is out.
    /// </summary>
    public async Task MarkAsync(string status, CancellationToken cancellationToken = default)
    {
        if (_record is not { } record || record.Status == status)
        {
            return;
        }

        _record = record with { Status = status };
        try
        {
            await _writer.WriteJsonAsync(TableRecord.File, _record, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException or Azure.RequestFailedException)
        {
            Console.WriteLine($"  Session {SessionId} could not be marked {status}: {failure.Message}");
        }
    }

    private TableRecord? _record;

    /// <summary>
    /// The agent the driver plays for a seat: the seat itself, wrapped so every decision it makes is a step.
    /// It goes <em>around</em> the seat rather than inside it, so a handover swaps who is seated without
    /// swapping the recording out with them.
    /// </summary>
    /// <remarks>
    /// The seat is also what says who decides each step: it is the thing a handover or a swap changes, so
    /// asking it is asking the one place that knows. Working it out here instead would be a second copy of the
    /// seating rule, kept in a file that has no reason to be edited when the seating changes. It answers with
    /// the occupant as well as the name, so the recorder asks whoever it names rather than reading the seat a
    /// second time.
    /// </remarks>
    public IPlayerAgent Wrap(MatchId matchId, SeatAgent seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        return _recorder.Wrap(matchId, seat, board =>
        {
            var decider = seat.Deciding(board);
            Reseated(board.Slot, decider.Name, board.RoundNumber);

            // The journal goes around the occupant, inside the seat: the seat still says who is sitting and
            // applies its swaps, and the journal answers for them while it is replaying and writes after them
            // once it is not.
            return decider with { Agent = Journal.Around(decider.Agent, board.Slot) };
        });
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
    public void Served(PlayerSlot slot, HumanSeat.Question? question) => _served.Served(slot, question);

    /// <summary>
    /// When this seat's current options were served, read before the decision is handed over so a question
    /// asked in its wake cannot take the moment with it.
    /// </summary>
    public DateTimeOffset? ServedAt(PlayerSlot slot, HumanSeat.Question? question) => _served.ServedAt(slot, question);

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
    /// Rewrites the trace as it stands. Called after every accepted decision, which is cheap next to a person
    /// thinking and is the difference between an abandoned session being readable and being nothing.
    /// </summary>
    /// <remarks>
    /// It does nothing once the session has been closed, and that is not an optimisation. A decision can still
    /// be in flight when the match ends -- the request thread parks on a board query behind the driver's own
    /// writes, the host closes the session, and only then does the request reach here -- and by that point
    /// <see cref="MatchTraceRecorder.Complete" /> has handed the finished trace over and forgotten the match.
    /// Writing then would truncate the real trace to an empty one. The recorder answering null for a match it
    /// no longer holds is what makes that impossible rather than merely unlikely.
    /// </remarks>
    /// <summary>How far the trace has got, so a caller can tell when something it set in motion has reached it.</summary>
    public int TraceLength(MatchId matchId) => Trace(matchId).Count;

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

    /// <summary>
    /// Waits until the trace has grown past <paramref name="beyond" />, or until the wait runs out.
    /// </summary>
    /// <remarks>
    /// A decision is applied on the driver's own thread, and the seat holding it is released asynchronously --
    /// so the request thread that accepted the tap runs on ahead of the command it caused. This is half of
    /// catching up with it: the first event of that command appearing is what says the command has *started*.
    /// The caller pairs it with the table's own gate to learn that the command has finished. The bound is
    /// there so a driver that stalls costs a stale trace rather than a player's tap hanging.
    /// </remarks>
    public async Task WaitForTraceAsync(MatchId matchId, int beyond, CancellationToken cancellationToken = default)
    {
        // Not past the closing either: once the trace is kept, the recorder forgets the match and its length
        // reads as nothing from then on.
        for (var waited = TimeSpan.Zero; _events.Length(matchId) <= beyond && _kept is null && waited < GrowthWait; waited += GrowthStep)
        {
            await Task.Delay(GrowthStep, _clock, cancellationToken);
        }
    }

    public Task CheckpointAsync(MatchId matchId, CancellationToken cancellationToken = default)
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

            return _events.Snapshot(matchId, _stamp, _seed) is { } trace
                ? _writer.WriteJsonAsync($"{RunRecorder.TracesDirectory}/{matchId}.json", trace, cancellationToken)
                : Task.CompletedTask;
        }
    }

    /// <summary>
    /// Closes the session: the episodes, the final trace, and the manifest with its counts. The board is the
    /// one the match ended on, which is what an episode's return is read from.
    /// </summary>
    public async Task FinishAsync(MatchId matchId, PlayerBoardState player1Board, CancellationToken cancellationToken = default)
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

        await _recorder.MatchPlayedAsync(matchId, _seed, player1Board, cancellationToken);
        await _recorder.FinishAsync(cancellationToken);
        await Task.WhenAny(Journal.Written);
        await MarkAsync(TableRecord.Finished, cancellationToken);

        // And only now are the files written. Saying so at the top of this method would let the session page
        // answer while the manifest still says the match played nothing, or while a file it is about to embed
        // is half written. Whether anything is still on its way into them is the other half of IsClosed.
        FilesWritten = true;
    }

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
