using DownfallArena.Application.Agents;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Agents;

public sealed class ActionScorerTests
{
    private static readonly ActionScorer Scorer = new(TestContent.Resources, MatchStore.TwoOnTwo(), ScoringWeights.Default);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    [Fact]
    public void The_expected_score_weighs_the_critical_branch_by_the_crit_chance()
    {
        // Strike deals 3, a crit 6; the actor crits 5% of the time.
        Scorer.Expected(Strike(One, Three), Board(enemyHealth: 20)).ShouldBe((0.95 * 3) + (0.05 * 6), 1e-9);
    }

    [Fact]
    public void A_kill_adds_the_kill_weight_to_the_effective_damage()
    {
        // Three health left: the effective damage is 3 in both branches, plus the kill.
        Scorer.Expected(Strike(One, Three), Board(enemyHealth: 3)).ShouldBe(3 + 5, 1e-9);
        Scorer.Kills(Strike(One, Three), Board(enemyHealth: 3)).ShouldBeTrue();
        Scorer.Kills(Strike(One, Three), Board(enemyHealth: 20)).ShouldBeFalse();
    }

    [Fact]
    public void A_bleed_counts_as_future_damage_capped_by_the_health_left_after_the_hit()
    {
        var board = Board(enemyHealth: 20, actorSpells: [TestContent.Strike, TestContent.Rend]);
        var full = (0.95 * (1 + (0.8 * 19))) + (0.05 * (2 + (0.8 * 18)));
        Scorer.Expected(Action(One, TestContent.Rend, Three), board).ShouldBe(full, 1e-9);

        var low = Board(enemyHealth: 3, actorSpells: [TestContent.Strike, TestContent.Rend]);
        Scorer.Expected(Action(One, TestContent.Rend, Three), low).ShouldBe((0.95 * (1 + (0.8 * 2))) + (0.05 * (2 + (0.8 * 1))), 1e-9);
    }

    [Fact]
    public void A_fizzle_costs_the_risk_weight()
    {
        // Slam is not known: the resolution fizzles in both branches.
        Scorer.Expected(Action(One, TestContent.Slam, Three), Board(enemyHealth: 20)).ShouldBe(-2, 1e-9);
    }

    [Fact]
    public void Healing_counts_what_was_missing_and_counts_against_when_it_heals_an_enemy()
    {
        var board = Board(enemyHealth: 20);
        var ally = Boards.Creature(2, PlayerSlot.Player1) with { Health = Health.Of(15) };
        var creatures = new List<CreatureSnapshot> { board[0], ally, board[1], board[2] };
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [ally.Id], [], false, Energy.Of(0), [new HealOutcome(ally.Id, 10)]), creatures).ShouldBe(0.8 * 5, 1e-9);
        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), [new HealOutcome(Three, 10)]), creatures).ShouldBe(0, 1e-9);
        var hurtEnemy = creatures.Select(creature => creature.Id == Three ? creature with { Health = Health.Of(12) } : creature).ToList();
        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), [new HealOutcome(Three, 10)]), hurtEnemy).ShouldBe(-0.8 * 8, 1e-9);
    }

    /// <summary>
    /// ADR 0020: energy handed out is priced at the energy weight, whole, because energy has no cap to waste
    /// it against. Without this an <see cref="EnergyOutcome"/> scored zero and a spell whose whole point is
    /// energy looked worthless to the agent.
    /// </summary>
    [Fact]
    public void Energy_given_to_an_ally_counts_for_at_the_energy_weight()
    {
        var board = Board(enemyHealth: 20);
        var ally = Boards.Creature(2, PlayerSlot.Player1);
        var creatures = new List<CreatureSnapshot> { board[0], ally, board[1], board[2] };
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [ally.Id], [], false, Energy.Of(0), [new EnergyOutcome(ally.Id, 3)]), creatures).ShouldBe(0.2 * 3, 1e-9);
    }

    [Fact]
    public void Energy_given_to_an_enemy_counts_against()
    {
        var board = Board(enemyHealth: 20);
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), [new EnergyOutcome(Three, 3)]), board).ShouldBe(-0.2 * 3, 1e-9);
    }

    [Fact]
    public void An_energyRegeneration_is_priced_at_the_energy_weight_over_the_rounds_it_lasts()
    {
        var board = Board(enemyHealth: 20);
        var ally = Boards.Creature(2, PlayerSlot.Player1);
        var creatures = new List<CreatureSnapshot> { board[0], ally, board[1], board[2] };
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [ally.Id], [], false, Energy.Of(0), [new ConditionOutcome(ally.Id, EnergyRegeneration.Of(2, rounds: 3))]), creatures).ShouldBe(0.2 * 2 * 3, 1e-9);
        Scorer.Score(CombatResolution.Resolved(action, [ally.Id], [], false, Energy.Of(0), [new ConditionOutcome(ally.Id, EnergyRegeneration.Of(2, rounds: 1))]), creatures).ShouldBe(0.2 * 2, 1e-9);
        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), [new ConditionOutcome(Three, EnergyRegeneration.Of(2, rounds: 3))]), creatures).ShouldBe(-0.2 * 2 * 3, 1e-9);
    }

    [Fact]
    public void Energy_kept_counts_a_little_and_dropped_targets_cost_risk()
    {
        var board = Board(enemyHealth: 20, actorEnergy: 2);
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), []), board).ShouldBe(0.2 * 2, 1e-9);
        Scorer.Score(CombatResolution.Resolved(action, [Three], [new TargetingFailure(Four, CombatErrors.NoTargets)], false, Energy.Of(0), []), board).ShouldBe((0.2 * 2) - 2, 1e-9);
    }

    [Fact]
    public void Best_picks_the_target_set_with_the_highest_score()
    {
        var board = Board(enemyHealth: 20, actorEnergy: 2, actorSpells: [TestContent.Strike, TestContent.Slam]);
        board[1] = board[1] with { Health = Health.Of(3) };

        var strike = Scorer.Best(board[0], TestContent.Strike, board).ShouldNotBeNull();
        strike.Targets.ShouldBe([Three]);
        strike.Score.ShouldBe(3 + 5 + (0.2 * 2), 1e-9);

        var slam = Scorer.Best(board[0], TestContent.Slam, board).ShouldNotBeNull();
        slam.Targets.ShouldBe([Three, Four]);
        slam.Score.ShouldBeGreaterThan(strike.Score);
    }

    [Fact]
    public void Estimate_values_a_spell_as_if_it_were_known_and_affordable()
    {
        var board = Board(enemyHealth: 20);

        Scorer.Estimate(board[0], TestContent.Slam, board).ShouldBe((0.95 * 10) + (0.05 * 14), 1e-9);
        Scorer.Estimate(board[0], TestContent.Guard, board).ShouldBe(1, 1e-9);
        Scorer.Estimate(board[0], TestContent.Strike, board).ShouldBe((0.95 * 3) + (0.05 * 6), 1e-9);
    }

    /// <summary>
    /// An unlock is worth what the spell does plus the initiative it buys (ADR 0017), priced by the initiative
    /// weight (ADR 0018). Here Guard is a Spell initiative of 6 and the others 1, and the weight is 0.5.
    /// </summary>
    [Fact]
    public void Unlocking_a_spell_is_worth_its_combat_value_plus_the_initiative_it_buys()
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20);

        scorer.UnlockValue(board[0], TestContent.Guard, board).ShouldBe(1 + 3, 1e-9);
        scorer.UnlockValue(board[0], TestContent.Strike, board).ShouldBe((0.95 * 3) + (0.05 * 6) + 0.5, 1e-9);
        scorer.UnlockValue(board[0], TestContent.Slam, board).ShouldBe((0.95 * 10) + (0.05 * 14) + 0.5, 1e-9);
    }

    /// <summary>
    /// Guard is worth 1 in combat against Strike's 3.15 and still wins the pick once the initiative it buys is
    /// priced. This is what it means for a pick to buy tempo, and it is the whole point of the weight.
    /// </summary>
    [Fact]
    public void A_spell_worth_less_in_combat_can_still_be_the_better_unlock()
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20);

        scorer.Estimate(board[0], TestContent.Guard, board)
            .ShouldBeLessThan(scorer.Estimate(board[0], TestContent.Strike, board));
        scorer.UnlockValue(board[0], TestContent.Guard, board)
            .ShouldBeGreaterThan(scorer.UnlockValue(board[0], TestContent.Strike, board));
    }

    /// <summary>At a weight of zero an unlock is worth exactly what it does in combat, and tempo buys nothing.</summary>
    [Fact]
    public void An_initiative_weight_of_zero_prices_an_unlock_at_its_combat_value_alone()
    {
        var board = Board(enemyHealth: 20);
        var indifferent = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default with { Initiative = 0 });

        indifferent.UnlockValue(board[0], TestContent.Guard, board)
            .ShouldBe(indifferent.Estimate(board[0], TestContent.Guard, board), 1e-9);
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var board = Board(enemyHealth: 20);

        Should.Throw<ArgumentNullException>(() => Scorer.Expected(null!, board));
        Should.Throw<ArgumentNullException>(() => Scorer.Expected(Strike(One, Three), null!));
        Should.Throw<ArgumentNullException>(() => Scorer.Best(null!, TestContent.Strike, board));
        Should.Throw<ArgumentNullException>(() => Scorer.Estimate(board[0], null!, board));
        Should.Throw<ArgumentNullException>(() => Scorer.UnlockValue(board[0], null!, board));
        Should.Throw<ArgumentNullException>(() => Scorer.Score(null!, board));
        Should.Throw<ArgumentNullException>(() => Scorer.Kills(Strike(One, Three), null!));
        Scorer.Weights.ShouldBe(ScoringWeights.Default);
    }

    private static CombatAction Strike(CreatureId actor, CreatureId target) => Action(actor, TestContent.Strike, target);

    private static CombatAction Action(CreatureId actor, SpellId spell, CreatureId target) => CombatAction.Bind(new CombatIntent(actor, spell), [target]);

    /// <summary>Creature 1 (player 1) facing creatures 3 and 4 (player 2), both at the given health.</summary>
    private static List<CreatureSnapshot> Board(int enemyHealth, int actorEnergy = 0, IReadOnlyList<SpellId>? actorSpells = null)
    {
        var actor = Boards.Creature(1, PlayerSlot.Player1) with
        {
            Energy = Energy.Of(actorEnergy),
            KnownSpells = new HashSet<SpellId>(actorSpells ?? [TestContent.Strike]),
        };
        return [actor, Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) }, Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) }];
    }
}
