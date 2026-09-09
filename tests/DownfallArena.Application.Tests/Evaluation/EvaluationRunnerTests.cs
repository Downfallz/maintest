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

    /// <summary>
    /// A spell every side declares sits at one half by construction: it is on the winning side and the losing
    /// side of every match. That is the reading the table rests on — one half means no signal.
    /// </summary>
    [Fact]
    public async Task A_spell_every_side_declares_scores_one_half()
    {
        var evaluation = await EvaluateAsync([1, 2, 3, 4], withCombat: true);

        var everywhere = evaluation.SpellOutcomes.Where(outcome => outcome.Sides == evaluation.Matches * 2).ToList();

        everywhere.ShouldNotBeEmpty("a starting spell is declared by every side");
        everywhere.ShouldAllBe(outcome => outcome.Score == 0.5);
    }

    [Fact]
    public async Task Every_spell_outcome_accounts_for_each_side_that_declared_it()
    {
        var evaluation = await EvaluateAsync([5, 6, 7], withCombat: true);

        evaluation.SpellOutcomes.ShouldNotBeEmpty();
        foreach (var outcome in evaluation.SpellOutcomes)
        {
            outcome.Sides.ShouldBe(outcome.Wins + outcome.Losses + outcome.Draws);
            outcome.Sides.ShouldBeGreaterThan(0);
            outcome.Intents.ShouldBeGreaterThanOrEqualTo(outcome.Sides, "a side that declared it cast it at least once");
            outcome.Score.ShouldBeInRange(0, 1);
            outcome.IntentsPerSide.ShouldBe((double)outcome.Intents / outcome.Sides, 1e-9);
        }

        evaluation.SpellOutcomes.Select(outcome => outcome.Score).ShouldBeInOrder(SortDirection.Descending);
    }

    /// <summary>The intents of the two agent reports and the spell table count the same declarations.</summary>
    [Fact]
    public async Task The_spell_table_counts_the_same_intents_as_the_agent_reports()
    {
        var evaluation = await EvaluateAsync([8, 9], withCombat: true);

        var fromReports = evaluation.AgentA.SpellUsage.Values.Sum() + evaluation.AgentB.SpellUsage.Values.Sum();

        evaluation.SpellOutcomes.Sum(outcome => outcome.Intents).ShouldBe(fromReports);
    }

    /// <summary>
    /// Two agents of the same spec are seeded alike, so the mirrored pass replays the same matches. The result
    /// says so, because the even split that follows is arithmetic rather than a measurement.
    /// </summary>
    [Fact]
    public async Task An_evaluation_of_an_agent_against_itself_is_flagged_as_self_play()
    {
        var evaluation = await EvaluateAsync([1, 2, 3], withCombat: true);

        evaluation.SelfPlay.ShouldBeTrue();
        evaluation.AgentA.WinRate.Mean.ShouldBe(0.5, 1e-9);
        evaluation.AgentB.WinRate.Mean.ShouldBe(0.5, 1e-9);
        evaluation.AgentA.SpellUsage.ShouldBe(evaluation.AgentB.SpellUsage);
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
