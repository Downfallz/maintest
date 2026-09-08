using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Tests.Evaluation;

public sealed class EvaluationRunnerTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo(roundCap: 6);
    private static readonly RunStamp Stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, Rules, FeatureSchema.Build(TestContent.Resources, Rules), "Random", "Random", 1);

    [Fact]
    public async Task Every_seed_is_played_twice_with_the_agents_swapped()
    {
        var evaluation = await EvaluateAsync([11, 12, 13], withCombat: true);

        evaluation.Matches.ShouldBe(6);
        evaluation.Pairs.Select(pair => pair.Seed).ShouldBe([11, 12, 13]);
        evaluation.Pairs.ShouldAllBe(pair => pair.AFirst.Seed == pair.Seed && pair.BFirst.Seed == pair.Seed);
        evaluation.Pairs.ShouldAllBe(pair => pair.AFirst.MatchId != pair.BFirst.MatchId);
        evaluation.Pairs.ShouldAllBe(pair => pair.WinsOfA + pair.WinsOfB + pair.Draws == 2);
        (evaluation.AgentA.Wins + evaluation.AgentB.Wins + evaluation.Draws).ShouldBe(6);
        evaluation.AverageRounds.ShouldBeInRange(1, 6);
        evaluation.RoundCapShare.ShouldBeInRange(0, 1);
        evaluation.Stamp.ShouldBe(Stamp);
        evaluation.AgentA.Agent.ShouldBe("Random");
    }

    [Fact]
    public async Task Reports_carry_the_paired_interval_the_spell_usage_and_the_combat_rates()
    {
        var evaluation = await EvaluateAsync([1, 2, 3, 4], withCombat: true);

        foreach (var report in new[] { evaluation.AgentA, evaluation.AgentB })
        {
            report.WinRate.Mean.ShouldBe(report.Wins / 8.0, 1e-9);
            report.WinRate.Low.ShouldBeLessThanOrEqualTo(report.WinRate.Mean);
            report.WinRate.High.ShouldBeGreaterThanOrEqualTo(report.WinRate.Mean);
            report.SpellUsage.ShouldNotBeEmpty();
            report.SpellUsage.Values.Sum().ShouldBeGreaterThan(0);
            report.SpellEntropy.ShouldBeGreaterThanOrEqualTo(0);
            report.Actions.ShouldBeGreaterThan(0);
            report.Fizzles.ShouldBeLessThanOrEqualTo(report.Actions);
            report.Criticals.ShouldBeLessThanOrEqualTo(report.Actions);
            report.AverageRemainingHealth.ShouldBeGreaterThanOrEqualTo(0);
        }

        (evaluation.AgentA.Score.Mean + evaluation.AgentB.Score.Mean).ShouldBe(1, 1e-9);
        evaluation.AgentA.SpellUsage.Values.Sum().ShouldBe(evaluation.AgentA.Actions, "every declared intent resolves once, fizzled or not");
    }

    [Fact]
    public async Task Without_a_combat_listener_the_rates_read_zero()
    {
        var evaluation = await EvaluateAsync([1, 2], withCombat: false);

        evaluation.AgentA.Actions.ShouldBe(0);
        evaluation.AgentA.FizzleRate.ShouldBe(0);
        evaluation.AgentA.CriticalRate.ShouldBe(0);
    }

    [Fact]
    public async Task The_same_seeds_give_the_same_digest_twice()
    {
        var first = BenchmarkDigest.Of(await EvaluateAsync([5, 6, 7], withCombat: false));
        var second = BenchmarkDigest.Of(await EvaluateAsync([5, 6, 7], withCombat: false));

        first.DifferencesFrom(second).ShouldBeEmpty();
        first.Entries.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Invalid_inputs_are_rejected()
    {
        var store = new MatchStore();
        var runner = new EvaluationRunner(Handlers.Runner(store.Workflow, new TestRandomFactory()));

        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(null!, Stamp, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(Scenario([1]), null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(Scenario([]), Stamp, TestContext.Current.CancellationToken));
    }

    private static async Task<EvaluationResult> EvaluateAsync(IReadOnlyList<int> seeds, bool withCombat)
    {
        var store = new MatchStore();
        var combat = new CombatStatsRecorder(store.Repository);
        var workflow = withCombat ? store.WorkflowWith(combat) : store.Workflow;
        var runner = new EvaluationRunner(Handlers.Runner(workflow, new TestRandomFactory()), withCombat ? combat : null);
        return await runner.RunAsync(Scenario(seeds), Stamp, TestContext.Current.CancellationToken);
    }

    private static EvaluationScenario Scenario(IReadOnlyList<int> seeds) => new()
    {
        RuleSet = Rules,
        Roster = MatchStore.Roster(Rules),
        AgentA = AgentSpec.Random,
        AgentB = AgentSpec.Random,
        Seeds = seeds,
    };
}
