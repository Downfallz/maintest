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

        var everywhere = evaluation.SpellOutcomes.Where(outcome => outcome.Sides == SidesOf(evaluation)).ToList();

        everywhere.ShouldNotBeEmpty("a starting spell is declared by every side");
        everywhere.ShouldAllBe(outcome => outcome.Score == 0.5);
    }

    /// <summary>
    /// In self-play the mirrored pass replays the same matches, so counting both would double every side
    /// without adding one independent observation — and the sample is what the table's confidence rests on.
    /// </summary>
    [Fact]
    public async Task Self_play_counts_the_replayed_matches_once()
    {
        int[] seeds = [1, 2, 3, 4];

        var evaluation = await EvaluateAsync(seeds, withCombat: true);

        evaluation.SelfPlay.ShouldBeTrue();
        evaluation.Matches.ShouldBe(seeds.Length * 2, "both batches are played");
        var everywhere = evaluation.SpellOutcomes.Where(outcome => outcome.Score == 0.5 && outcome.Sides == SidesOf(evaluation)).ToList();
        everywhere.ShouldNotBeEmpty();
        everywhere.ShouldAllBe(outcome => outcome.Sides == seeds.Length * 2, "one side per player of each seed, not of each replay");
        evaluation.SpellOutcomes.ShouldAllBe(outcome => outcome.Sides <= seeds.Length * 2);
    }

    /// <summary>The most sides a spell can be declared by: one per player of each independent match.</summary>
    private static int SidesOf(EvaluationResult evaluation) =>
        evaluation.SelfPlay ? evaluation.Matches : evaluation.Matches * 2;

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

    /// <summary>
    /// The agent reports count every match played; the spell table counts every match that is evidence. In
    /// self-play those differ by exactly the replayed batch, which is the whole point of dropping it.
    /// </summary>
    [Fact]
    public async Task The_spell_table_counts_the_intents_of_the_matches_it_treats_as_evidence()
    {
        var evaluation = await EvaluateAsync([8, 9], withCombat: true);

        var fromReports = evaluation.AgentA.SpellUsage.Values.Sum() + evaluation.AgentB.SpellUsage.Values.Sum();
        var fromTable = evaluation.SpellOutcomes.Sum(outcome => outcome.Intents);

        evaluation.SelfPlay.ShouldBeTrue();
        fromTable.ShouldBe(fromReports / 2, "self-play replays each match once more, and the table counts it once");
        (fromReports % 2).ShouldBe(0, "the replay declares exactly what the first pass declared");
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

    [Fact]
    public async Task What_a_spell_did_is_counted_beside_how_often_it_was_chosen()
    {
        var evaluation = await EvaluateAsync([5, 6, 7], withCombat: true);

        foreach (var outcome in evaluation.SpellOutcomes)
        {
            (outcome.Resolved + outcome.Fizzled).ShouldBeLessThanOrEqualTo(
                outcome.Intents,
                "a declaration can go unresolved when the creature dies before acting, never the other way round");
            outcome.ResolvedWhenWon.ShouldBeLessThanOrEqualTo(outcome.Resolved);
            outcome.ResolveRate.ShouldBeInRange(0, 1);
            outcome.CastShareWhenWon.ShouldBeInRange(0, 1);
        }

        evaluation.SpellOutcomes.Sum(outcome => outcome.Resolved).ShouldBeGreaterThan(0, "the run resolved something");
        evaluation.SpellOutcomes.Sum(outcome => outcome.Damage).ShouldBeGreaterThan(0, "and something landed");

        // The test content's Slam stuns, Rend bleeds and Guard buffs, so a run that casts them proves each
        // condition kind reaches its own column rather than all of them landing in one.
        var conditions = evaluation.SpellOutcomes.Sum(outcome => outcome.Stuns + outcome.Bleeds + outcome.Buffs);
        conditions.ShouldBeGreaterThan(0, "the content's conditions are counted, not dropped");
    }

    /// <summary>
    /// The table's totals are the combat recorder's totals for the matches it treats as evidence, which is what
    /// makes "what this spell did" the same quantity the fizzle rate is computed from.
    /// </summary>
    [Fact]
    public async Task The_casts_the_table_counts_are_the_casts_the_agent_reports()
    {
        var evaluation = await EvaluateAsync([5, 6, 7], withCombat: true);

        var fromTable = evaluation.SpellOutcomes.Sum(outcome => outcome.Resolved + outcome.Fizzled);
        var fromReports = evaluation.AgentA.Actions + evaluation.AgentB.Actions;

        evaluation.SelfPlay.ShouldBeTrue();
        fromTable.ShouldBe(fromReports / 2, "self-play replays each match once more, and the table counts it once");
    }

    [Fact]
    public async Task An_evaluation_without_a_combat_recorder_reports_no_effects_rather_than_wrong_ones()
    {
        var evaluation = await EvaluateAsync([5, 6], withCombat: false);

        evaluation.SpellOutcomes.ShouldNotBeEmpty();
        evaluation.SpellOutcomes.ShouldAllBe(outcome => outcome.Resolved == 0 && outcome.Damage == 0);
        evaluation.SpellOutcomes.ShouldAllBe(outcome => outcome.Sides > 0, "the sides are counted without it");
    }

    /// <summary>
    /// That the credited damage is the damage that landed is pinned where it is decided, in
    /// <see cref="CombatStatsRecorderTests"/>; a run where nothing overkills totals the same either way. What
    /// is worth checking here is that the per-cast figure is the total divided by the casts it came from.
    /// </summary>
    [Fact]
    public async Task Damage_per_cast_is_the_damage_over_the_casts_it_came_from()
    {
        var evaluation = await EvaluateAsync([5, 6, 7], withCombat: true);

        foreach (var outcome in evaluation.SpellOutcomes.Where(outcome => outcome.Resolved > 0))
        {
            outcome.DamagePerCast.ShouldBe((double)outcome.Damage / outcome.Resolved, 1e-9);
        }

        evaluation.SpellOutcomes.ShouldAllBe(outcome => outcome.Damage >= 0 && outcome.Healing >= 0);
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
