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
/// <c>traces/&lt;match&gt;.json</c> per match. Matches are closed explicitly by the runner, once their final board
/// is known, rather than on the <c>MatchEnded</c> event: the return needs the board.
/// </summary>
public sealed class RunRecorder(
    IArtifactWriter writer,
    RunStamp stamp,
    ObservationBuilder observations,
    ActionEncoder actions,
    TimeProvider timeProvider,
    MatchTraceRecorder? traces = null) : IMatchRecorder
{
    public const string ManifestFile = "manifest.json";
    public const string StepsFile = "steps.jsonl";
    public const string EpisodesFile = "episodes.jsonl";
    public const string TracesDirectory = "traces";

    private readonly Dictionary<MatchId, List<StepRecord>> _steps = [];
    private readonly DateTimeOffset _createdAt = timeProvider.GetUtcNow();

    public RunStamp Stamp => stamp;

    public int Matches { get; private set; }

    public int Steps { get; private set; }

    public int Episodes { get; private set; }

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

        return new RecordingAgent(agent, observations, actions, steps);
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
            await writer.WriteJsonAsync($"{TracesDirectory}/{matchId}.json", tracer.Complete(matchId, stamp, seed), cancellationToken);
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
                Matches = Matches,
                Steps = Steps,
                Episodes = Episodes,
                Traces = traces is not null,
            },
            cancellationToken);
}
