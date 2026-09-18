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

    private readonly IArtifactWriter _writer;
    private readonly RunRecorder _recorder;
    private readonly MatchTraceRecorder _events;
    private readonly RunStamp _stamp;
    private readonly TimeProvider _clock;
    private readonly DecisionClock _served;
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
    /// Whether the dataset has been closed: the episodes written and the manifest rewritten with its counts.
    /// Until then the directory holds the zero-count manifest it was opened with, so a page that showed the
    /// session would be showing a run that says it played nothing.
    /// </summary>
    public bool IsClosed { get; private set; }

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
    /// A decision was accepted, timed from when this seat's options were served. The question it answered is
    /// named so the clock cannot hand back the stamp of the next one.
    /// </summary>
    public Task DecidedAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, HumanSeat.Question? answered, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Decision(Where(matchId, slot, round, subPhase), _served.Answered(slot, answered), _clock), cancellationToken);

    /// <summary>
    /// A decision was refused. The clock is left alone: the seat is still being asked the same question, and
    /// the time a player spent being refused is part of how long that question took them.
    /// </summary>
    public Task RefusedAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, DomainError error, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Refused(Where(matchId, slot, round, subPhase), error, _clock), cancellationToken);

    /// <summary>A note a player produced with one tap, or typed on the end screen.</summary>
    public Task TypedAsync(MatchId matchId, PlayerSlot slot, int? round, RoundSubPhase? subPhase, NoteKind kind, string text, CancellationToken cancellationToken = default) =>
        NoteAsync(PlaytestNote.Typed(Where(matchId, slot, round, subPhase), kind, text, _clock), cancellationToken);

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
    public async Task CheckpointAsync(MatchId matchId, CancellationToken cancellationToken = default)
    {
        if (_events.Snapshot(matchId, _stamp, _seed) is { } trace)
        {
            await _writer.WriteJsonAsync($"{RunRecorder.TracesDirectory}/{matchId}.json", trace, cancellationToken);
        }
    }

    /// <summary>
    /// Closes the session: the episodes, the final trace, and the manifest with its counts. The board is the
    /// one the match ended on, which is what an episode's return is read from.
    /// </summary>
    public async Task FinishAsync(MatchId matchId, PlayerBoardState player1Board, CancellationToken cancellationToken = default)
    {
        await _recorder.MatchPlayedAsync(matchId, _seed, player1Board, cancellationToken);
        await _recorder.FinishAsync(cancellationToken);
        IsClosed = true;
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
