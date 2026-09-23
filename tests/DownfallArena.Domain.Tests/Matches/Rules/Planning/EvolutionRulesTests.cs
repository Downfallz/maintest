using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

public sealed class EvolutionRulesTests
{
    private static readonly TierId Unknown = TierId.Parse("tier:nobody:v1");

    [Fact]
    public void A_valid_choice_targets_an_own_living_creature_with_an_available_package_and_a_pick_left()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, round).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(9), Arena.GuardPack), creatures, round).Error.ShouldBe(PlanningErrors.UnknownCreature);
        Validate(PlayerSlot.Player2, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, round).Error.ShouldBe(PlanningErrors.NotYourCreature);
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Unknown), creatures, round).Error.ShouldBe(PlanningErrors.UnknownTier);
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.SlamPack), creatures, round).Error.ShouldBe(PlanningErrors.TierNotAvailable);
    }

    /// <summary>
    /// The package is what blocks a repeat, not the spells it teaches: a creature that knows every spell of a
    /// package it never bought may still buy it, and one that owns it may not buy it again (ADR 0056).
    /// </summary>
    [Fact]
    public void A_package_already_owned_is_refused_and_knowing_its_spells_is_not_owning_it()
    {
        var living = Arena.FourCreatures();
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        living[0].Learn(Arena.Guard);
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), Arena.Snapshots(living), round).IsSuccess.ShouldBeTrue();

        living[0].BuyTier(Arena.Resources.GetTier(Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), Arena.Snapshots(living), round).Error.ShouldBe(PlanningErrors.TierAlreadyOwned);
    }

    /// <summary>
    /// The prerequisite is owned by the creature, not by the team: the Slam package opens for the creature
    /// that bought Guard and for no other.
    /// </summary>
    [Fact]
    public void A_package_opens_only_for_the_creature_that_owns_its_prerequisite()
    {
        var living = Arena.FourCreatures();
        var round = Arena.RoundAt(RoundSubPhase.Evolution);
        living[0].BuyTier(Arena.Resources.GetTier(Arena.GuardPack));

        var creatures = Arena.Snapshots(living);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.SlamPack), creatures, round).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Archer, Arena.SlamPack), creatures, round).Error.ShouldBe(PlanningErrors.TierNotAvailable);
    }

    [Fact]
    public void A_dead_creature_cannot_evolve()
    {
        var living = Arena.FourCreatures();
        living[0].TakeDamage(99);
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), Arena.Snapshots(living), round).Error.ShouldBe(PlanningErrors.CreatureDead);
    }

    [Fact]
    public void Picks_are_limited_by_the_rule_set()
    {
        var living = Arena.FourCreatures();
        var creatures = Arena.Snapshots(living);
        var round = Arena.RoundAt(RoundSubPhase.Evolution);
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack));
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Archer, Arena.GuardPack));

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.SlamPack), creatures, round).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        Validate(PlayerSlot.Player2, new EvolutionChoice(Arena.Ghoul, Arena.GuardPack), creatures, round).IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// A round the schedule gives no opportunity answers the same way as a round whose picks are spent, and
    /// the phase is complete the moment it opens: nobody is asked for an input they do not have (ADR 0056).
    /// </summary>
    [Fact]
    public void A_round_the_schedule_skips_offers_no_pick_at_all()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var second = Arena.RoundAt(RoundSubPhase.Evolution, number: 2);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, second).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        EvolutionRules.Evaluate(creatures, second, Arena.Resources, RuleSet.Default).ShouldBe(new EvolutionGateResult(true, 0, 0));

        var third = Arena.RoundAt(RoundSubPhase.Evolution, number: 3);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, third).IsSuccess.ShouldBeTrue();
        EvolutionRules.Evaluate(creatures, third, Arena.Resources, RuleSet.Default).CanAdvance.ShouldBeFalse();
    }

    /// <summary>The schedule is the rule set's, so a rule set that offers every round is answered that way.</summary>
    [Fact]
    public void A_rule_set_with_an_interval_of_one_offers_every_round()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var every = RuleSet.Create(3, 2, 2, 30, 2.0, firstEvolutionRound: 1, evolutionInterval: 1);
        var second = Arena.RoundAt(RoundSubPhase.Evolution, number: 2);

        EvolutionRules.ValidateChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, second, Arena.Resources, every)
            .IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Each creature buys at most one package a round (ADR 0066), so a pick counts only while a living creature
    /// that has not bought is left to spend it on: once the Knight has bought and its ally is dead, Player 1's
    /// second pick is gone, and a player down to one creature has one pick.
    /// </summary>
    [Fact]
    public void The_gate_counts_remaining_picks_capped_by_the_creatures_that_can_still_buy()
    {
        var living = Arena.FourCreatures();
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        var fresh = EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default);
        fresh.CanAdvance.ShouldBeFalse();
        fresh.RemainingPicksOf(PlayerSlot.Player1).ShouldBe(2);
        fresh.RemainingPicksOf(PlayerSlot.Player2).ShouldBe(2);

        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack));
        living[0].BuyTier(Arena.Resources.GetTier(Arena.GuardPack));

        EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default).Player1RemainingPicks.ShouldBe(1);

        living[1].TakeDamage(99);

        EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default).Player1RemainingPicks.ShouldBe(0, "the Knight has bought and its only ally is dead");

        round.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Arena.Ghoul, Arena.GuardPack));
        round.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Arena.Wraith, Arena.GuardPack));

        var done = EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default);
        done.CanAdvance.ShouldBeTrue();
        done.ShouldBe(new EvolutionGateResult(true, 0, 0));
    }

    [Fact]
    public void A_player_down_to_one_creature_has_one_pick()
    {
        var living = Arena.FourCreatures();
        living[1].TakeDamage(99);

        EvolutionRules.Evaluate(Arena.Snapshots(living), Arena.RoundAt(RoundSubPhase.Evolution), Arena.Resources, RuleSet.Default)
            .RemainingPicksOf(PlayerSlot.Player1).ShouldBe(1);
    }

    [Fact]
    public void A_creature_that_has_bought_this_round_is_refused_a_second_package()
    {
        var living = Arena.FourCreatures();
        var round = Arena.RoundAt(RoundSubPhase.Evolution);
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack));
        living[0].BuyTier(Arena.Resources.GetTier(Arena.GuardPack));

        EvolutionRules.ValidateChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.SlamPack), Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default)
            .Error.ShouldBe(PlanningErrors.CreatureAlreadyEvolved);
    }

    [Fact]
    public void The_gate_advances_when_nothing_is_left_to_buy_even_with_picks_left()
    {
        var living = Arena.FourCreatures();
        foreach (var creature in living)
        {
            creature.BuyTier(Arena.Resources.GetTier(Arena.GuardPack));
            creature.BuyTier(Arena.Resources.GetTier(Arena.SlamPack));
        }

        var gate = EvolutionRules.Evaluate(Arena.Snapshots(living), Arena.RoundAt(RoundSubPhase.Evolution), Arena.Resources, RuleSet.Default);

        gate.CanAdvance.ShouldBeTrue();
    }

    [Fact]
    public void A_player_who_passed_has_no_pick_left()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var round = Arena.RoundAt(RoundSubPhase.Evolution);
        round.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.GuardPack), creatures, round).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        var gate = EvolutionRules.Evaluate(creatures, round, Arena.Resources, RuleSet.Default);
        gate.ShouldBe(new EvolutionGateResult(false, 0, 2));

        round.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        EvolutionRules.Evaluate(creatures, round, Arena.Resources, RuleSet.Default).CanAdvance.ShouldBeTrue();
    }

    private static Result Validate(PlayerSlot slot, EvolutionChoice choice, IReadOnlyList<CreatureSnapshot> creatures, Round round) =>
        EvolutionRules.ValidateChoice(slot, choice, creatures, round, Arena.Resources, RuleSet.Default);
}
