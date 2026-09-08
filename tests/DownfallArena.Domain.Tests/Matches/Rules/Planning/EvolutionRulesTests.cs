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
    [Fact]
    public void A_valid_choice_targets_an_own_living_creature_with_an_unlockable_spell_and_a_pick_left()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Guard), creatures, round).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(9), Arena.Guard), creatures, round).Error.ShouldBe(PlanningErrors.UnknownCreature);
        Validate(PlayerSlot.Player2, new EvolutionChoice(Arena.Knight, Arena.Guard), creatures, round).Error.ShouldBe(PlanningErrors.NotYourCreature);
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Strike), creatures, round).Error.ShouldBe(PlanningErrors.SpellAlreadyKnown);
        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Slam), creatures, round).Error.ShouldBe(PlanningErrors.SpellNotUnlockable);
    }

    [Fact]
    public void A_dead_creature_cannot_evolve()
    {
        var living = Arena.FourCreatures();
        living[0].TakeDamage(99);
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Guard), Arena.Snapshots(living), round).Error.ShouldBe(PlanningErrors.CreatureDead);
    }

    [Fact]
    public void Picks_are_limited_by_the_rule_set()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var round = Arena.RoundAt(RoundSubPhase.Evolution);
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Guard));
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Archer, Arena.Guard));

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Slam), creatures, round).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        Validate(PlayerSlot.Player2, new EvolutionChoice(Arena.Ghoul, Arena.Guard), creatures, round).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_gate_counts_remaining_picks_capped_by_what_can_be_unlocked()
    {
        var living = Arena.FourCreatures();
        var round = Arena.RoundAt(RoundSubPhase.Evolution);

        var fresh = EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default);
        fresh.CanAdvance.ShouldBeFalse();
        fresh.RemainingPicksOf(PlayerSlot.Player1).ShouldBe(2);
        fresh.RemainingPicksOf(PlayerSlot.Player2).ShouldBe(2);

        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Guard));
        living[0].UnlockSpell(Arena.Guard);
        living[1].TakeDamage(99);

        var afterOnePick = EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default);
        afterOnePick.Player1RemainingPicks.ShouldBe(1);

        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Slam));
        living[0].UnlockSpell(Arena.Slam);
        round.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Arena.Ghoul, Arena.Guard));
        round.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Arena.Wraith, Arena.Guard));

        var done = EvolutionRules.Evaluate(Arena.Snapshots(living), round, Arena.Resources, RuleSet.Default);
        done.CanAdvance.ShouldBeTrue();
        done.ShouldBe(new EvolutionGateResult(true, 0, 0));
    }

    [Fact]
    public void The_gate_advances_when_nothing_is_left_to_unlock_even_with_picks_left()
    {
        var living = Arena.FourCreatures();
        foreach (var creature in living)
        {
            creature.UnlockSpell(Arena.Guard);
            creature.UnlockSpell(Arena.Slam);
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

        Validate(PlayerSlot.Player1, new EvolutionChoice(Arena.Knight, Arena.Guard), creatures, round).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        var gate = EvolutionRules.Evaluate(creatures, round, Arena.Resources, RuleSet.Default);
        gate.ShouldBe(new EvolutionGateResult(false, 0, 2));

        round.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        EvolutionRules.Evaluate(creatures, round, Arena.Resources, RuleSet.Default).CanAdvance.ShouldBeTrue();
    }

    private static Result Validate(PlayerSlot slot, EvolutionChoice choice, IReadOnlyList<CreatureSnapshot> creatures, Round round) =>
        EvolutionRules.ValidateChoice(slot, choice, creatures, round, Arena.Resources, RuleSet.Default);
}
