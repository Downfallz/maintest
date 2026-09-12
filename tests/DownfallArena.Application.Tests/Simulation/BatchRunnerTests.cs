using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Simulation;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Simulation;

public sealed class BatchRunnerTests
{
    [Fact]
    public async Task A_batch_plays_every_match_from_its_own_seed_and_summarizes()
    {
        var factory = new TestRandomFactory();
        var runner = Runner(new MatchStore(), factory);

        var batch = await runner.RunAsync(Scenario(matches: 3, baseSeed: 10), TestContext.Current.CancellationToken);

        batch.Results.Count.ShouldBe(3);
        batch.Results.Select(result => result.Index).ShouldBe([0, 1, 2]);
        batch.Results.Select(result => result.Seed).ShouldBe([10, 11, 12]);
        batch.Results.Select(result => result.MatchId).Distinct().Count().ShouldBe(3);
        batch.Results.ShouldAllBe(result => result.ContentHash == "test-content");
        batch.Results.ShouldAllBe(result => result.Rounds >= 1);
        batch.Results.ShouldAllBe(result => result.Outcome.Reason == MatchEndReason.RoundCap || Math.Min(result.Player1RemainingHealth, result.Player2RemainingHealth) == 0);
        // As a set: the matches play at the same time, so which one reaches the factory first is not fixed.
        // What is fixed is that each match seeds the board from its own seed and its two agents from that
        // seed, which is what makes a batch replay.
        factory.Seeds.ShouldBeSubsetOf([10, 11, 12, (10 * 31) + 1, (10 * 31) + 2, (11 * 31) + 1, (11 * 31) + 2, (12 * 31) + 1, (12 * 31) + 2]);
        factory.Seeds.ShouldContain(10);
        factory.Seeds.ShouldContain((10 * 31) + 1);
        factory.Seeds.ShouldContain((10 * 31) + 2);
        batch.Summary.Matches.ShouldBe(3);
        (batch.Summary.Player1Wins + batch.Summary.Player2Wins + batch.Summary.Draws).ShouldBe(3);
        batch.Summary.AverageRounds.ShouldBe(batch.Results.Average(result => result.Rounds));
        batch.Scenario.Matches.ShouldBe(3);
    }

    [Fact]
    public async Task The_same_scenario_replays_the_same_results()
    {
        var first = await Runner(new MatchStore(), new TestRandomFactory()).RunAsync(Scenario(matches: 2, baseSeed: 5), TestContext.Current.CancellationToken);
        var second = await Runner(new MatchStore(), new TestRandomFactory()).RunAsync(Scenario(matches: 2, baseSeed: 5), TestContext.Current.CancellationToken);

        Comparable(first).ShouldBe(Comparable(second));
    }

    [Fact]
    public async Task An_empty_batch_has_an_empty_summary()
    {
        var batch = await Runner(new MatchStore(), new TestRandomFactory()).RunAsync(Scenario(matches: 0, baseSeed: 1), TestContext.Current.CancellationToken);

        batch.Results.ShouldBeEmpty();
        batch.Summary.ShouldBe(SimulationSummary.Of([]));
        batch.Summary.Player1WinRate.ShouldBe(0);
    }

    [Fact]
    public async Task Invalid_scenarios_are_rejected()
    {
        var runner = Runner(new MatchStore(), new TestRandomFactory());

        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(Scenario(matches: -1, baseSeed: 1), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The matches of a batch are independent, so they play at the same time; the results are still the
    /// seeds' results in the seeds' order, because each lands in its own slot rather than being appended.
    /// </summary>
    [Fact]
    public async Task A_batch_reports_its_matches_in_seed_order_however_they_finish()
    {
        var runner = Runner(new MatchStore(), new TestRandomFactory());

        var batch = await runner.RunAsync(Scenario(matches: 16, baseSeed: 100), TestContext.Current.CancellationToken);

        batch.Results.Select(result => result.Index).ShouldBe([.. Enumerable.Range(0, 16)]);
        batch.Results.Select(result => result.Seed).ShouldBe([.. Enumerable.Range(100, 16)]);
    }

    /// <summary>
    /// A recorder that appends every match to one artifact cannot take matches at the same time: its lines
    /// would interleave and the run would stop replaying from its seed. It says so, and the batch obeys.
    /// </summary>
    [Fact]
    public async Task A_recorder_that_refuses_parallel_matches_sees_them_one_at_a_time()
    {
        var recorder = new CountingRecorder(allowsParallel: false);
        var runner = Runner(new MatchStore(), new TestRandomFactory());

        await runner.RunAsync(Scenario(matches: 16, baseSeed: 200), recorder, TestContext.Current.CancellationToken);

        recorder.Matches.ShouldBe(16);
        recorder.HighWater.ShouldBe(1, "a recorder that refuses parallel matches must never see two at once");
    }

    /// <summary>
    /// The other half of the test above: without this, "never two at once" would pass on a runner that
    /// never runs anything at once, and prove nothing about the switch.
    /// </summary>
    [Fact]
    public async Task A_recorder_that_allows_parallel_matches_sees_more_than_one_at_a_time()
    {
        var recorder = new CountingRecorder(allowsParallel: true);
        var runner = Runner(new MatchStore(), new TestRandomFactory());

        await runner.RunAsync(Scenario(matches: 64, baseSeed: 400), recorder, TestContext.Current.CancellationToken);

        recorder.Matches.ShouldBe(64);
        recorder.HighWater.ShouldBeGreaterThan(1, "the matches of a batch are independent and play at once");
    }

    [Fact]
    public async Task A_recorder_that_allows_parallel_matches_replays_the_same_results()
    {
        var sequential = await Runner(new MatchStore(), new TestRandomFactory())
            .RunAsync(Scenario(matches: 8, baseSeed: 300), new CountingRecorder(allowsParallel: false), TestContext.Current.CancellationToken);
        var parallel = await Runner(new MatchStore(), new TestRandomFactory())
            .RunAsync(Scenario(matches: 8, baseSeed: 300), new CountingRecorder(allowsParallel: true), TestContext.Current.CancellationToken);

        Comparable(parallel).ShouldBe(Comparable(sequential));
    }

    /// <summary>
    /// Counts the matches it saw and the most it ever had open at once.
    /// <para>
    /// Open is counted per match and not per `Wrap`: the runner wraps both agents of a match, so counting
    /// the calls would climb by one a match and read as parallel work on a batch that never overlapped.
    /// </para>
    /// </summary>
    private sealed class CountingRecorder(bool allowsParallel) : IMatchRecorder
    {
        private readonly Lock _gate = new();
        private readonly HashSet<MatchId> _open = [];

        public bool AllowsParallelMatches => allowsParallel;

        public int Matches { get; private set; }

        public int HighWater { get; private set; }

        public IPlayerAgent Wrap(MatchId matchId, IPlayerAgent agent)
        {
            lock (_gate)
            {
                _open.Add(matchId);
                HighWater = Math.Max(HighWater, _open.Count);
            }

            return agent;
        }

        public Task MatchPlayedAsync(MatchId matchId, int seed, PlayerBoardState player1Board, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _open.Remove(matchId);
                Matches++;
            }

            return Task.CompletedTask;
        }
    }

    private static SimulationScenario Scenario(int matches, int baseSeed) => new()
    {
        RuleSet = MatchStore.TwoOnTwo(roundCap: 8),
        Player1Roster = MatchStore.Roster(MatchStore.TwoOnTwo()),
        Player2Roster = MatchStore.Roster(MatchStore.TwoOnTwo()),
        Matches = matches,
        BaseSeed = baseSeed,
    };

    private static BatchRunner Runner(MatchStore store, TestRandomFactory factory) =>
        new(
            new CreateMatchHandler(store.Workflow, TestContent.Resources, factory),
            new JoinMatchHandler(store.Workflow),
            new GetBoardStateForPlayerHandler(store.Workflow),
            new MatchDriver(
                new MatchCommandHandlers(
                    new SubmitEvolutionChoiceHandler(store.Workflow),
                    new PassEvolutionHandler(store.Workflow),
                    new SubmitSpeedChoiceHandler(store.Workflow),
                    new SubmitIntentHandler(store.Workflow),
                    new SubmitActionHandler(store.Workflow),
                    new ResolveNextActionHandler(store.Workflow)),
                new MatchQueryHandlers(
                    new GetBoardStateForPlayerHandler(store.Workflow),
                    new GetPlayerOptionsHandler(store.Workflow, TestContent.Resources))),
            factory,
            Handlers.Agents());

    private static List<string> Comparable(BatchResult batch) =>
        [.. batch.Results.Select(result => $"{result.Seed}:{result.Outcome}:{result.Rounds}:{result.Player1RemainingHealth}:{result.Player2RemainingHealth}")];
}
