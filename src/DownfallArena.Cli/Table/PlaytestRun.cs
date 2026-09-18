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

    // How long a checkpoint waits for the decision it follows to reach the trace, and how often it looks. The
    // driver applies one command in microseconds, so this is all but always over on the first look; the bound
    // exists so that a driver which is not coming back costs a stale trace rather than a hanging tap.
    private static readonly TimeSpan GrowthWait = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan GrowthStep = TimeSpan.FromMilliseconds(5);

    private readonly IArtifactWriter _writer;
    private readonly RunRecorder _recorder;
    private readonly MatchTraceRecorder _events;
    private readonly RunStamp _stamp;
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
        string directory,
        IArtifactWriter writer,
        RunRecorder recorder,
        MatchTraceRecorder events,
        RunStamp stamp,
        TimeProvider clock,
        int seed)
    {
        Directory = directory;
        _writer = writer;
        _recorder = recorder;
        _events = events;
        _stamp = stamp;
        _clock = clock;
        _served = new DecisionClock(clock);
        _seed = seed;
    }

    /// <summary>
    /// The name of this session's directory, and the id every note carries. Read off the directory rather than
    /// kept beside it: they are the same thing, and two fields holding it are two fields that can disagree.
    /// </summary>
    public string SessionId => Path.GetFileName(Directory);

    /// <summary>Where the session is being written, which its players are told before the first tap.</summary>
    public string Directory { get; }

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
    public static PlaytestRun Open(string root, PlaytestSetup setup, MatchTraceRecorder events, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        var directory = Path.Combine(root, Name(clock));
        var writer = new FileArtifactWriter(directory);
        var schema = FeatureSchema.Build(setup.Resources, setup.Rules);
        var stamp = RunStamp.Create(EngineVersion.Current, setup.Resources, setup.Rules, schema, setup.Player1Agent, setup.Player2Agent, setup.Seed);

        // One trace, because a session is one match: the limit is what stops the recorder from being asked for
        // a second one it never had.
        var recorder = new RunRecorder(
            writer,
            stamp,
            new ObservationBuilder(schema, setup.Resources),
            new ActionEncoder(schema),
            new CandidateTerms(setup.Resources, setup.Rules),
            clock,
            events,
            traceLimit: 1);

        return new PlaytestRun(directory, writer, recorder, events, stamp, clock, setup.Seed);
    }

    /// <summary>
    /// Starts the dataset files and writes what this session is playing. The manifest lands with zero counts,
    /// so a session abandoned before its first decision still says what it was.
    /// </summary>
    public async Task StartAsync(CatalogueView catalogue, CancellationToken cancellationToken = default)
    {
        await _recorder.StartAsync(cancellationToken);
        await _writer.StartJsonLinesAsync(NotesFile, cancellationToken);
        await _writer.WriteJsonAsync(CatalogueFile, catalogue, cancellationToken);
    }

    /// <summary>
    /// The agent the driver plays for a seat: the seat itself, wrapped so every decision it makes is a step.
    /// It goes <em>around</em> the seat rather than inside it, so a handover swaps who is seated without
    /// swapping the recording out with them.
    /// </summary>
    public IPlayerAgent Wrap(MatchId matchId, SeatAgent seat) => _recorder.Wrap(matchId, seat);

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
    public int TraceLength(MatchId matchId) => _events.Length(matchId);

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
            ? final.Skip(Math.Clamp(since, 0, final.Count)).ToList()
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
        for (var waited = TimeSpan.Zero; _events.Length(matchId) <= beyond && waited < GrowthWait; waited += GrowthStep)
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

        // And only now are the files written. Saying so at the top of this method would let the session page
        // answer while the manifest still says the match played nothing, or while a file it is about to embed
        // is half written. Whether anything is still on its way into them is the other half of IsClosed.
        FilesWritten = true;
    }

    /// <summary>
    /// The session's files, as the viewer reads them. The trace comes first because the viewer opens on the
    /// first artifact it is handed and the thing two people want the moment they finish is the match they just
    /// played; the dataset files follow. <c>notes.jsonl</c> and <c>catalogue.json</c> are handed over too and
    /// the viewer ignores both today, which is deliberate: it can learn to read them later without the files
    /// having to be invented then.
    /// </summary>
    public IReadOnlyList<(string Name, string Text)> Artifacts()
    {
        List<(string Name, string Text)> artifacts = [];
        var traces = Path.Combine(Directory, RunRecorder.TracesDirectory);
        if (System.IO.Directory.Exists(traces))
        {
            foreach (var trace in System.IO.Directory.GetFiles(traces, "*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                artifacts.Add(($"{RunRecorder.TracesDirectory}/{Path.GetFileName(trace)}", File.ReadAllText(trace)));
            }
        }

        foreach (var name in new[] { RunRecorder.ManifestFile, RunRecorder.EpisodesFile, RunRecorder.StepsFile, NotesFile, CatalogueFile })
        {
            var path = Path.Combine(Directory, name);
            if (File.Exists(path))
            {
                artifacts.Add((name, File.ReadAllText(path)));
            }
        }

        return artifacts;
    }

    /// <summary>
    /// A name that sorts by when it was played and cannot collide with a session started in the same second.
    /// The clock is the injected one, so a test names a session rather than racing one.
    /// </summary>
    private static string Name(TimeProvider clock) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{clock.GetUtcNow():yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(2))}");
}
