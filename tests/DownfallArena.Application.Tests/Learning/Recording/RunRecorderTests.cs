using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Simulation;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Learning.Recording;

public sealed class RunRecorderTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo(roundCap: 8);
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, Rules);
    private static readonly RunStamp Stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, Rules, Schema, "Random", "Random", 10);

    /// <summary>
    /// The one line that keeps a recorded run replayable: every match is appended to one `steps.jsonl`, so
    /// matches playing at once would interleave their lines. Without this the recorder would take a parallel
    /// batch (the default for a recorder that says nothing) and no other test would notice.
    /// </summary>
    [Fact]
    public void A_recorded_run_refuses_a_batch_that_plays_its_matches_at_once()
    {
        var recorder = Recorder(new MemoryArtifactWriter(), null);

        recorder.AllowsParallelMatches.ShouldBeFalse();
    }

    /// <summary>
    /// And the lines it writes are in one block per match, whatever the batch's degree: a step of match two
    /// between two steps of match one is a dataset that no longer replays from its seed.
    /// </summary>
    [Fact]
    public async Task A_recorded_run_keeps_each_match_is_steps_together()
    {
        var store = new MatchStore();
        var writer = new MemoryArtifactWriter();
        var recorder = Recorder(writer, null);
        var runner = Handlers.Runner(store.Workflow, new TestRandomFactory());

        await recorder.StartAsync(TestContext.Current.CancellationToken);
        await runner.RunAsync(Scenario(matches: 8), recorder, TestContext.Current.CancellationToken);
        await recorder.FinishAsync(TestContext.Current.CancellationToken);

        var matches = writer.LinesOf<StepRecord>(RunRecorder.StepsFile).Select(step => step.MatchId).ToList();
        matches.Distinct().Count().ShouldBe(8);
        matches.Distinct().Count().ShouldBe(Blocks(matches), "the steps of a match must not be interleaved with another's");
        writer.LinesOf<EpisodeRecord>(RunRecorder.EpisodesFile).Count.ShouldBe(16, "one episode per side of each match");
    }

    /// <summary>How many runs of the same value the list falls into, so 8 matches in 8 blocks is contiguous.</summary>
    private static int Blocks(List<MatchId> matches) =>
        matches.Count == 0 ? 0 : 1 + matches.Zip(matches.Skip(1)).Count(pair => pair.First != pair.Second);

    [Fact]
    public async Task A_recorded_batch_writes_the_manifest_the_steps_the_episodes_and_the_traces()
    {
        var store = new MatchStore();
        var tracer = new MatchTraceRecorder(store.Repository);
        var writer = new MemoryArtifactWriter();
        var recorder = Recorder(writer, tracer);
        var runner = Handlers.Runner(store.WorkflowWith(tracer), new TestRandomFactory());

        await writer.AppendJsonLinesAsync(RunRecorder.StepsFile, ["a line of an earlier run"], TestContext.Current.CancellationToken);
        await recorder.StartAsync(TestContext.Current.CancellationToken);
        writer.Document<RunManifest>(RunRecorder.ManifestFile).Matches.ShouldBe(0);
        writer.LinesOf<object>(RunRecorder.StepsFile).ShouldBeEmpty();
        writer.LinesOf<object>(RunRecorder.EpisodesFile).ShouldBeEmpty();

        var batch = await runner.RunAsync(Scenario(matches: 2), recorder, TestContext.Current.CancellationToken);
        await recorder.FinishAsync(TestContext.Current.CancellationToken);

        var manifest = writer.Document<RunManifest>(RunRecorder.ManifestFile);
        manifest.Stamp.ShouldBe(Stamp);
        manifest.CreatedAt.ShouldBe(FixedTimeProvider.Default);
        manifest.SchemaId.ShouldBe(Schema.Id);
        manifest.SchemaVersion.ShouldBe("features:v3");
        manifest.FeatureNames.ShouldBe(Schema.FeatureNames);
        manifest.Matches.ShouldBe(2);
        manifest.Traces.ShouldBeTrue();

        var steps = writer.LinesOf<StepRecord>(RunRecorder.StepsFile);
        var episodes = writer.LinesOf<EpisodeRecord>(RunRecorder.EpisodesFile);
        manifest.Steps.ShouldBe(steps.Count);
        manifest.Episodes.ShouldBe(episodes.Count);
        steps.ShouldNotBeEmpty();
        episodes.Count.ShouldBe(4);
        recorder.Matches.ShouldBe(2);
        recorder.Steps.ShouldBe(steps.Count);
        recorder.Episodes.ShouldBe(4);

        foreach (var result in batch.Results)
        {
            var own = episodes.Where(episode => episode.MatchId == result.MatchId).ToList();
            own.Select(episode => episode.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
            own.Sum(episode => episode.Return).ShouldBe(0.0, 1e-9);
            own.Sum(episode => episode.Steps).ShouldBe(steps.Count(step => step.MatchId == result.MatchId));
            own.ShouldAllBe(episode => episode.Seed == result.Seed && episode.Outcome == result.Outcome && episode.Rounds == result.Rounds);
            own[0].RemainingHealth.ShouldBe(result.Player1RemainingHealth);
            own[0].EnemyRemainingHealth.ShouldBe(result.Player2RemainingHealth);
            own[1].RemainingHealth.ShouldBe(result.Player2RemainingHealth);

            var trace = writer.Document<MatchTrace>($"{RunRecorder.TracesDirectory}/{result.MatchId}.json");
            trace.MatchId.ShouldBe(result.MatchId);
            trace.Seed.ShouldBe(result.Seed);
            trace.Stamp.ShouldBe(Stamp);
            trace.Outcome.ShouldBe(result.Outcome);
            trace.Entries.ShouldNotBeEmpty();
            tracer.EntriesOf(result.MatchId).ShouldBeEmpty();
        }

        writer.Writes.Take(4).ShouldBe([RunRecorder.StepsFile, RunRecorder.StepsFile, RunRecorder.EpisodesFile, RunRecorder.ManifestFile]);
        writer.Writes[^1].ShouldBe(RunRecorder.ManifestFile);
    }

    [Fact]
    public async Task Without_a_trace_recorder_only_the_dataset_is_written()
    {
        var store = new MatchStore();
        var writer = new MemoryArtifactWriter();
        var recorder = Recorder(writer, traces: null);

        await Handlers.Runner(store.Workflow, new TestRandomFactory()).RunAsync(Scenario(matches: 1), recorder, TestContext.Current.CancellationToken);
        await recorder.FinishAsync(TestContext.Current.CancellationToken);

        writer.Documents.Keys.ShouldBe([RunRecorder.ManifestFile]);
        writer.Document<RunManifest>(RunRecorder.ManifestFile).Traces.ShouldBeFalse();
        writer.LinesOf<EpisodeRecord>(RunRecorder.EpisodesFile).Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_match_played_without_wrapped_agents_still_closes_with_two_empty_episodes()
    {
        var store = new MatchStore();
        var match = store.Started(Rules, new TestRandom(3));
        await Handlers.Driver(store.Workflow).PlayAsync(match.Id, new RandomAgent(new TestRandom(1)), new RandomAgent(new TestRandom(2)), TestContext.Current.CancellationToken);
        var board = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var writer = new MemoryArtifactWriter();
        var recorder = Recorder(writer, traces: null);

        await recorder.MatchPlayedAsync(match.Id, 7, board, TestContext.Current.CancellationToken);

        var episodes = writer.LinesOf<EpisodeRecord>(RunRecorder.EpisodesFile);
        episodes.Count.ShouldBe(2);
        episodes.ShouldAllBe(episode => episode.Steps == 0 && episode.Seed == 7);
        writer.LinesOf<StepRecord>(RunRecorder.StepsFile).ShouldBeEmpty();
        recorder.Matches.ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_inputs_are_rejected()
    {
        var recorder = Recorder(new MemoryArtifactWriter(), traces: null);
        var unfinished = PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);
        var player2View = unfinished with { Slot = PlayerSlot.Player2 };

        Should.Throw<ArgumentNullException>(() => recorder.Wrap(MatchId.New(), null!));
        await Should.ThrowAsync<ArgumentNullException>(() => recorder.MatchPlayedAsync(MatchId.New(), 1, null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => recorder.MatchPlayedAsync(unfinished.MatchId, 1, unfinished, TestContext.Current.CancellationToken));
        Should.Throw<ArgumentException>(() => EpisodeRecord.Of(PlayerSlot.Player1, player2View, 0, null));
        Should.Throw<ArgumentOutOfRangeException>(() => EpisodeRecord.Of(PlayerSlot.Player1, unfinished, -1, null));
        Should.Throw<ArgumentNullException>(() => EpisodeRecord.Of(PlayerSlot.Player1, null!, 0, null));
    }

    private static RunRecorder Recorder(MemoryArtifactWriter writer, MatchTraceRecorder? traces) =>
        new(writer, Stamp, new ObservationBuilder(Schema, TestContent.Resources), new ActionEncoder(Schema), new FixedTimeProvider(FixedTimeProvider.Default), traces);

    private static SimulationScenario Scenario(int matches) => new()
    {
        RuleSet = Rules,
        Player1Roster = MatchStore.Roster(Rules),
        Player2Roster = MatchStore.Roster(Rules),
        Matches = matches,
        BaseSeed = 10,
    };
}
