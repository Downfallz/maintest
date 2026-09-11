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
    private static readonly CreatureId Two = CreatureId.From(2);
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

    /// <summary>
    /// An outcome on a creature the same action kills never lands: <c>Heal</c> and <c>GainEnergy</c> both
    /// return zero on a dead creature. Scored anyway, energy handed to a dying enemy would be a penalty the
    /// action never pays, and a heal on a dying ally a bonus it never gets. The condition path already gated
    /// on this; these two did not.
    /// </summary>
    [Fact]
    public void An_outcome_on_a_creature_the_action_kills_scores_nothing()
    {
        var board = Board(enemyHealth: 3);
        var ally = Boards.Creature(2, PlayerSlot.Player1) with { Health = Health.Of(3) };
        var creatures = new List<CreatureSnapshot> { board[0], ally, board[1], board[2] };
        var action = Strike(One, Three);
        var kill = new DamageOutcome(Three, 3, Critical: false);

        // 3 effective damage plus the kill weight, and nothing at all for the energy the corpse never gains.
        Scorer.Score(CombatResolution.Resolved(action, [Three], [], false, Energy.Of(0), [kill, new EnergyOutcome(Three, 3)]), creatures)
            .ShouldBe(3 + 5, 1e-9);

        // The same for a heal on an ally this action's own damage finishes off.
        Scorer.Score(CombatResolution.Resolved(action, [ally.Id], [], false, Energy.Of(0), [new DamageOutcome(ally.Id, 3, Critical: false), new HealOutcome(ally.Id, 10)]), creatures)
            .ShouldBe(-(3 + 5), 1e-9);
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

    /// <summary>
    /// Every lasting effect is priced over the rounds it lasts, and this asks all of them at once.
    ///
    /// The two it was written for were both priced flat: a three-round stun was worth a one-round stun, and
    /// Infectious Blast's two-round initiative debuff was worth Ice Spear's one-round one. `rounds` was
    /// computed on the line above them and read by the other three effects, which is what made the omission
    /// invisible -- the switch was exhaustive over the *types* and not over what each type carries.
    ///
    /// The sibling test below holds it to the whole taxonomy, so the effect added next fails here rather than
    /// scoring zero through <c>ConditionScore</c>'s default arm and nobody noticing.
    /// </summary>
    [Theory]
    [MemberData(nameof(LastingEffects))]
    public void A_lasting_effect_is_worth_more_the_longer_it_lasts(string name)
    {
        // The theory takes the effect's name and looks the pair up here, rather than carrying the effects
        // themselves: xUnit serializes theory data to enumerate rows, and a LastingEffect does not serialize.
        var (brief, long_) = Durations.First(pair => pair.Brief.GetType().Name == name);

        // Hurt, because a heal over time on a target at full health is worth nothing however long it runs --
        // correctly, and it would make this ask the wrong question of Regeneration.
        var board = Board(enemyHealth: 20);
        var hurt = board[1] with { Health = Health.Of(10) };
        var creatures = new List<CreatureSnapshot> { board[0], hurt, board[2] };
        var action = Strike(One, hurt.Id);

        var one = Scorer.Score(Cast(action, hurt.Id, brief), creatures);
        var two = Scorer.Score(Cast(action, hurt.Id, long_), creatures);

        Math.Abs(two).ShouldBeGreaterThan(Math.Abs(one), $"{name} lasts twice as long and is priced the same");
    }

    /// <summary>
    /// The sweep names its effects, so this is what keeps the naming honest: every concrete
    /// <see cref="LastingEffect"/> the domain declares has to be in it. Without this, a new effect compiles,
    /// falls through <c>ConditionScore</c>'s default arm, scores zero, and the theory above stays green.
    /// </summary>
    [Fact]
    public void The_duration_sweep_covers_every_lasting_effect_the_domain_declares()
    {
        var declared = typeof(LastingEffect).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(LastingEffect)))
            .Select(type => type.Name);

        declared.ShouldBeSubsetOf(Durations.Select(pair => pair.Brief.GetType().Name));
    }

    /// <summary>The name of each lasting effect the sweep covers; the pair itself is looked up by the test.</summary>
    public static TheoryData<string> LastingEffects()
    {
        var data = new TheoryData<string>();
        foreach (var (brief, _) in Durations)
        {
            data.Add(brief.GetType().Name);
        }

        return data;
    }

    private static IReadOnlyList<(LastingEffect Brief, LastingEffect Long)> Durations { get; } =
    [
        (Stun.For(1), Stun.For(2)),
        (Bleed.Of(1, rounds: 1), Bleed.Of(1, rounds: 2)),
        (Regeneration.Of(1, rounds: 1), Regeneration.Of(1, rounds: 2)),
        (EnergyRegeneration.Of(1, rounds: 1), EnergyRegeneration.Of(1, rounds: 2)),
        (DefenseBuff.Of(1, Duration.OfRounds(1)), DefenseBuff.Of(1, Duration.OfRounds(2))),
        (InitiativeDebuff.Of(1, Duration.OfRounds(1)), InitiativeDebuff.Of(1, Duration.OfRounds(2))),
    ];

    /// <summary>
    /// The sweep only asks that a longer effect scores further from zero, which a sign error would survive.
    /// These two are the exact prices, in the same shape as the energy regeneration test above: a stun on an
    /// enemy counts for, an initiative debuff on an enemy counts for, and both multiply by their rounds.
    /// </summary>
    [Fact]
    public void A_stun_and_an_initiative_debuff_are_priced_per_round_against_an_enemy()
    {
        var board = Board(enemyHealth: 20);
        var action = Strike(One, Three);

        Scorer.Score(Cast(action, Three, Stun.For(1)), board).ShouldBe(3.0, 1e-9);
        Scorer.Score(Cast(action, Three, Stun.For(2)), board).ShouldBe(3.0 * 2, 1e-9);
        Scorer.Score(Cast(action, Three, InitiativeDebuff.Of(1, Duration.OfRounds(2))), board).ShouldBe(0.5 * 1 * 2, 1e-9);
        Scorer.Score(Cast(action, Three, InitiativeDebuff.Of(2, Duration.OfRounds(3))), board).ShouldBe(0.5 * 2 * 3, 1e-9);
    }

    private static CombatResolution Cast(CombatAction action, CreatureId target, LastingEffect effect) =>
        CombatResolution.Resolved(action, [target], [], false, Energy.Of(0), [new ConditionOutcome(target, effect)]);

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
        // Guard is 2 defense for a round, and the actor faces two attackers with no ally to spread them over:
        // two hits of it prevented, priced at the buff weight (ADR 0022).
        Scorer.Estimate(board[0], TestContent.Guard, board).ShouldBe(0.5 * 2 * 2, 1e-9);
        Scorer.Estimate(board[0], TestContent.Strike, board).ShouldBe((0.95 * 3) + (0.05 * 6), 1e-9);
    }

    /// <summary>
    /// An unlock is worth what the spell does plus the initiative it buys (ADR 0017), priced by the initiative
    /// weight (ADR 0018). Here Guard is a Spell initiative of 6 and the others 1, and the weight is 0.5.
    /// </summary>
    [Fact]
    public void Unlocking_a_spell_is_worth_its_combat_value_plus_the_initiative_it_buys_less_what_it_costs()
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20);

        // The board starts at 0 energy, so the whole cost is the part the estimate cannot see. Energy is 0.2 a
        // point, so the three costs -- Strike 0, Guard 1, Slam 2 -- price at 0, 0.2 and 0.4.
        scorer.UnlockValue(board[0], TestContent.Guard, board).ShouldBe(2 + 3 - 0.2, 1e-9);
        scorer.UnlockValue(board[0], TestContent.Strike, board).ShouldBe((0.95 * 3) + (0.05 * 6) + 0.5, 1e-9);
        scorer.UnlockValue(board[0], TestContent.Slam, board).ShouldBe((0.95 * 10) + (0.05 * 14) + 0.5 - 0.4, 1e-9);
    }

    /// <summary>
    /// Guard is worth 2 in combat against Strike's 3.15 and still wins the pick once the initiative it buys is
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
    public void An_initiative_weight_of_zero_prices_an_unlock_at_its_combat_value_less_its_cost()
    {
        var board = Board(enemyHealth: 20);
        var indifferent = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default with { Initiative = 0 });

        indifferent.UnlockValue(board[0], TestContent.Guard, board)
            .ShouldBe(indifferent.Estimate(board[0], TestContent.Guard, board) - 0.2, 1e-9);
    }

    /// <summary>
    /// Both weights at zero leaves the combat value alone, which is the seam the other two are measured from.
    /// </summary>
    [Fact]
    public void An_energy_weight_of_zero_prices_an_unlock_at_its_combat_value_alone()
    {
        var board = Board(enemyHealth: 20);
        var free = new ActionScorer(
            TestContent.GuardIsFaster,
            MatchStore.TwoOnTwo(),
            ScoringWeights.Default with { Initiative = 0, Energy = 0 });

        free.UnlockValue(board[0], TestContent.Guard, board)
            .ShouldBe(free.Estimate(board[0], TestContent.Guard, board), 1e-9);
    }

    /// <summary>
    /// The case the term exists for (ADR 0026). <see cref="ActionScorer.Estimate"/> raises the actor's energy
    /// to at least the spell's cost so an unaffordable spell can still be read in combat, and the score counts
    /// the energy it *keeps* -- so a creature handed exactly what the spell costs keeps nothing whatever the
    /// spell costs, and two spells it cannot afford used to price the same.
    /// </summary>
    [Fact]
    public void A_creature_that_cannot_afford_an_unlock_still_prices_what_it_will_cost()
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20);
        board[0].Energy.Value.ShouldBe(0, "the case is about a creature that can afford neither");

        var free = scorer.UnlockValue(board[0], TestContent.Strike, board);
        var paid = scorer.UnlockValue(board[0], TestContent.Slam, board);

        (free - scorer.Estimate(board[0], TestContent.Strike, board)).ShouldBe(0.5, 1e-9);
        (paid - scorer.Estimate(board[0], TestContent.Slam, board)).ShouldBe(0.5 - 0.4, 1e-9);
    }

    /// <summary>
    /// The other half of the same term, and the one the first version of ADR 0026 got wrong. Once the actor
    /// can afford the spell, <see cref="ActionScorer.Estimate"/>'s raise does nothing and the energy it keeps
    /// already differs by the full cost -- so charging the cost again here would price it twice. At 4 energy
    /// the unlock is worth its combat value plus its initiative and nothing else.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void A_creature_that_can_afford_an_unlock_is_not_charged_for_it_twice(int energy)
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20, actorEnergy: energy);

        foreach (var spell in new[] { TestContent.Strike, TestContent.Guard, TestContent.Slam })
        {
            var initiative = spell == TestContent.Guard ? 0.5 * 6 : 0.5;
            (scorer.UnlockValue(board[0], spell, board) - scorer.Estimate(board[0], spell, board))
                .ShouldBe(initiative, 1e-9, $"{spell} costs at most {energy}, so its cost is already in the estimate");
        }
    }

    /// <summary>Between the two: at 1 energy, Slam's 2 is half affordable, and only the half that is not is charged.</summary>
    [Fact]
    public void Only_the_part_of_a_cost_a_creature_cannot_cover_is_charged_at_the_unlock()
    {
        var scorer = new ActionScorer(TestContent.GuardIsFaster, MatchStore.TwoOnTwo(), ScoringWeights.Default);
        var board = Board(enemyHealth: 20, actorEnergy: 1);

        (scorer.UnlockValue(board[0], TestContent.Slam, board) - scorer.Estimate(board[0], TestContent.Slam, board))
            .ShouldBe(0.5 - 0.2, 1e-9);
    }

    /// <summary>
    /// ADR 0022: a heal that lifts its target out of reach of the round's threat denies a kill, and a denied
    /// kill is worth what taking one is worth. Two enemies threaten 3.15 each, so an ally at 5 dies this round
    /// and an ally at 15 does not.
    /// </summary>
    [Fact]
    public void A_heal_that_takes_an_ally_out_of_reach_of_the_round_is_worth_the_kill_it_denies()
    {
        var action = Strike(One, Three);

        var dying = WithAlly(Health.Of(5));
        Scorer.Score(CombatResolution.Resolved(action, [Two], [], false, Energy.Of(0), [new HealOutcome(Two, 3)]), dying)
            .ShouldBe((0.8 * 3) + 5, 1e-9);

        var safe = WithAlly(Health.Of(15));
        Scorer.Score(CombatResolution.Resolved(action, [Two], [], false, Energy.Of(0), [new HealOutcome(Two, 3)]), safe)
            .ShouldBe(0.8 * 3, 1e-9);
    }

    /// <summary>
    /// A heal that leaves its target inside the round's threat denies nothing: 5 health and 3 restored is
    /// still under the 6.3 coming. The term is for the heal that saves a life, not for every heal on a hurt
    /// creature.
    /// </summary>
    [Fact]
    public void A_heal_that_does_not_reach_past_the_threat_is_worth_only_the_healing()
    {
        var action = Strike(One, Three);
        var dying = WithAlly(Health.Of(3));

        Scorer.Score(CombatResolution.Resolved(action, [Two], [], false, Energy.Of(0), [new HealOutcome(Two, 3)]), dying)
            .ShouldBe(0.8 * 3, 1e-9);
    }

    /// <summary>
    /// ADR 0022: defense comes off every incoming hit, so a buff is worth the hits the creature is expected to
    /// face while it lasts. Two attackers spread over two living allies is one hit a round; over one ally it
    /// is two, and the same buff is worth twice as much.
    /// </summary>
    [Fact]
    public void A_defense_buff_is_worth_more_to_a_team_the_attackers_have_fewer_targets_to_spread_over()
    {
        var action = Strike(One, Three);
        var buff = new ConditionOutcome(One, DefenseBuff.Of(1, Duration.OfRounds(2)));

        var pair = WithAlly(Health.Of(20));
        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [buff]), pair)
            .ShouldBe(0.5 * 1 * 2 * (2 / 2.0), 1e-9);

        var alone = WithAlly(Health.Of(0));
        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [buff]), alone)
            .ShouldBe(0.5 * 1 * 2 * (2 / 1.0), 1e-9);
    }

    /// <summary>
    /// The same term that makes a heal worth a kill makes a buff worth one: 2 defense turns two hits of 3.15
    /// into two of 1.15, which a creature at 6 survives and would not otherwise.
    /// </summary>
    [Fact]
    public void A_defense_buff_that_survives_a_lethal_round_is_worth_the_kill_it_denies()
    {
        var board = Board(enemyHealth: 20);
        board[0] = board[0] with { Health = Health.Of(6) };
        var action = Strike(One, Three);
        var buff = new ConditionOutcome(One, DefenseBuff.Of(2, Duration.OfRounds(1)));

        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [buff]), board)
            .ShouldBe((0.5 * 2 * 1 * 2) + 5, 1e-9);
    }

    /// <summary>Nothing can hit the creature, so nothing is prevented and the buff is worth nothing.</summary>
    [Fact]
    public void A_defense_buff_is_worth_nothing_when_no_living_enemy_can_reach_the_target()
    {
        var board = Board(enemyHealth: 20);
        board[1] = board[1] with { Health = Health.Of(0) };
        board[2] = board[2] with { Health = Health.Of(0) };
        var action = Strike(One, Three);

        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [new ConditionOutcome(One, DefenseBuff.Of(2, Duration.OfRounds(1)))]), board)
            .ShouldBe(0, 1e-9);
    }

    /// <summary>
    /// ADR 0022 leaves regeneration out of the survival term on purpose: it ticks at the end of a round, so it
    /// cannot save a creature from the threat of that round. The same ally a heal of 3 is worth a kill on gets
    /// only the healing here.
    /// </summary>
    [Fact]
    public void A_regeneration_on_a_dying_ally_is_worth_its_healing_and_no_kill()
    {
        var action = Strike(One, Three);
        var dying = WithAlly(Health.Of(5));

        Scorer.Score(CombatResolution.Resolved(action, [Two], [], false, Energy.Of(0), [new ConditionOutcome(Two, Regeneration.Of(3, rounds: 1))]), dying)
            .ShouldBe(0.8 * 3, 1e-9);
    }

    /// <summary>
    /// A point of defense is not a point prevented. Against a 3-damage spell on a creature already at 3
    /// defense the plain hit is fully absorbed, so another point only reaches the critical branch and is worth
    /// the critical chance times what it takes off there -- 0.05 of 2, twice over, not a whole point per
    /// attacker. Priced per point of buff, repeated Guards would be paid for damage they never prevent.
    /// </summary>
    [Fact]
    public void A_defense_buff_is_worth_what_it_takes_off_the_threat_not_a_point_per_point()
    {
        var board = Board(enemyHealth: 20);
        board[0] = board[0] with { TotalDefense = Defense.Of(3) };
        var action = Strike(One, Three);
        var buff = new ConditionOutcome(One, DefenseBuff.Of(1, Duration.OfRounds(1)));

        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [buff]), board)
            .ShouldBe(0.5 * 2 * (0.05 * (3 - 2)), 1e-9);
    }

    /// <summary>
    /// A cast can deny one death per target, and Guard carries two defense effects. Read per outcome, either
    /// each of them would claim the kill the other already denied, or -- when survival needs both -- neither
    /// would claim it. Here one point alone takes the round below lethal, and the kill is still paid once.
    /// </summary>
    [Fact]
    public void Two_defense_effects_on_one_target_deny_one_kill_between_them()
    {
        var board = Board(enemyHealth: 20);
        board[0] = board[0] with { Health = Health.Of(5) };
        var action = Strike(One, Three);
        var buff = new ConditionOutcome(One, DefenseBuff.Of(1, Duration.OfRounds(1)));

        // 6.3 coming and 5 health: lethal. Stacked, the two points take it to 2.3, and each point is priced on
        // top of the other rather than both from the bare board.
        Scorer.Score(CombatResolution.Resolved(action, [One], [], false, Energy.Of(0), [buff, buff]), board)
            .ShouldBe((0.5 * (6.3 - 2.3)) + 5, 1e-9);
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

    /// <summary>The same board with an ally of creature 1 at the given health, dead at zero.</summary>
    private static List<CreatureSnapshot> WithAlly(Health health)
    {
        var board = Board(enemyHealth: 20);
        var ally = Boards.Creature(2, PlayerSlot.Player1) with { Health = health };
        return [board[0], ally, board[1], board[2]];
    }

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
