using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// Records a run as a dataset (ADR 0013): <c>manifest.json</c> with the run stamp, one line per step in
/// <c>steps.jsonl</c>, one line per episode in <c>episodes.jsonl</c>, and when a trace recorder is given, one
/// <c>traces/&lt;match&gt;.json</c> per match up to <paramref name="traceLimit"/>. Matches are closed explicitly
/// by the runner, once their final board is known, rather than on the <c>MatchEnded</c> event: the return needs
/// the board.
/// </summary>
/// <param name="traceLimit">
/// How many match traces to write, newest matches dropped rather than oldest so a run always keeps the same
/// first few whatever its length. A trace is two board projections per event and about twenty times the size
/// of the steps a learner reads from the same match, so a dataset large enough to fit rare actions cannot
/// afford one per match: 1000 matches write about 70 MB of steps and 4.7 GB of traces. Nothing trains on a
/// trace -- it is the viewer's artifact (L3) -- so a handful of them is a sample, not a loss. Every match is
/// still handed to <see cref="MatchTraceRecorder.Complete"/> so the recorder forgets it; skipping that is how
/// a long run runs out of memory instead of disk.
/// </param>
public sealed class RunRecorder(
    IArtifactWriter writer,
    RunStamp stamp,
    ObservationBuilder observations,
    ActionEncoder actions,
    CandidateTerms terms,
    TimeProvider timeProvider,
    MatchTraceRecorder? traces = null,
    int traceLimit = int.MaxValue) : IMatchRecorder
{
    public const string ManifestFile = "manifest.json";
    public const string StepsFile = "steps.jsonl";
    public const string EpisodesFile = "episodes.jsonl";
    public const string TracesDirectory = "traces";

    private readonly Dictionary<MatchId, List<StepRecord>> _steps = [];
    private readonly DateTimeOffset _createdAt = timeProvider.GetUtcNow();

    /// <summary>
    /// No: every match is appended to one <c>steps.jsonl</c> and one <c>episodes.jsonl</c>, so matches playing
    /// at the same time would interleave their lines and a recorded run would no longer replay from its seed.
    /// A dataset that cannot be reproduced is not a dataset anything should be trained on.
    /// </summary>
    public bool AllowsParallelMatches => false;

    public RunStamp Stamp => stamp;

    public int Matches { get; private set; }

    public int Steps { get; private set; }

    public int Episodes { get; private set; }

    /// <summary>Match traces actually written, which <c>traceLimit</c> caps below <see cref="Matches"/>.</summary>
    public int Traces { get; private set; }

    /// <summary>
    /// Starts the dataset files empty and writes the manifest with zero counts, so a reused directory never mixes
    /// two runs and an interrupted run still says what it was.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await writer.StartJsonLinesAsync(StepsFile, cancellationToken);
        await writer.StartJsonLinesAsync(EpisodesFile, cancellationToken);
        await WriteManifestAsync(cancellationToken);
    }

    public IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (!_steps.TryGetValue(matchId, out var steps))
        {
            steps = [];
            _steps[matchId] = steps;
        }

        return new RecordingAgent(agent, observations, actions, terms, steps);
    }

    public async Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player1Board);

        _steps.Remove(matchId, out var steps);
        steps ??= [];
        var episodes = new[]
        {
            EpisodeRecord.Of(PlayerSlot.Player1, player1Board, steps.Count(step => step.Slot == PlayerSlot.Player1), seed),
            EpisodeRecord.Of(PlayerSlot.Player2, player1Board, steps.Count(step => step.Slot == PlayerSlot.Player2), seed),
        };

        await writer.AppendJsonLinesAsync(StepsFile, steps, cancellationToken);
        await writer.AppendJsonLinesAsync(EpisodesFile, episodes, cancellationToken);
        if (traces is { } tracer)
        {
            // Completed whether or not it is written: Complete is what makes the recorder forget the match.
            var trace = tracer.Complete(matchId, stamp, seed);
            if (Traces < traceLimit)
            {
                await writer.WriteJsonAsync($"{TracesDirectory}/{matchId}.json", trace, cancellationToken);
                Traces++;
            }
        }

        Matches++;
        Steps += steps.Count;
        Episodes += episodes.Length;
    }

    /// <summary>Writes the manifest with the final counts.</summary>
    public Task FinishAsync(CancellationToken cancellationToken = default) => WriteManifestAsync(cancellationToken);

    private Task WriteManifestAsync(CancellationToken cancellationToken) =>
        writer.WriteJsonAsync(
            ManifestFile,
            new RunManifest
            {
                Stamp = stamp,
                CreatedAt = _createdAt,
                SchemaId = observations.Schema.Id,
                SchemaVersion = observations.Schema.Version,
                FeatureNames = observations.Schema.FeatureNames,
                CandidateTermNames = CandidateTerms.Names,
                Matches = Matches,
                Steps = Steps,
                Episodes = Episodes,
                // Known before the first match rather than counted, so an interrupted run says the same thing
                // as a finished one: whether this run writes traces at all, not how far it got.
                Traces = traces is not null && traceLimit > 0,
            },
            cancellationToken);
}
