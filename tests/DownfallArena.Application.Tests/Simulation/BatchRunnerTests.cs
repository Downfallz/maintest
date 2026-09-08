using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Simulation;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;

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
        factory.Seeds.Take(3).ShouldBe([10, (10 * 31) + 1, (10 * 31) + 2]);
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
            new AgentFactory());

    private static List<string> Comparable(BatchResult batch) =>
        [.. batch.Results.Select(result => $"{result.Seed}:{result.Outcome}:{result.Rounds}:{result.Player1RemainingHealth}:{result.Player2RemainingHealth}")];
}
