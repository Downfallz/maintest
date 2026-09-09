using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Tests.Matches.Rules.Combat;

public sealed class IntentRulesTests
{
    [Fact]
    public void A_player_declares_a_known_affordable_spell_for_an_own_living_unstunned_creature()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Archer).TakeDamage(99);
        Arena.Find(living, Arena.Ghoul).Apply(Stun.For(1));
        Arena.Find(living, Arena.Wraith).UnlockSpell(Arena.SpellOf(Arena.Guard));
        var creatures = Arena.Snapshots(living);

        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Strike, creatures).IsSuccess.ShouldBeTrue();
        Validate(PlayerSlot.Player1, CreatureId.From(9), Arena.Strike, creatures).Error.ShouldBe(CombatErrors.UnknownCreature);
        Validate(PlayerSlot.Player2, Arena.Knight, Arena.Strike, creatures).Error.ShouldBe(CombatErrors.NotYourCreature);
        Validate(PlayerSlot.Player1, Arena.Archer, Arena.Strike, creatures).Error.ShouldBe(CombatErrors.ActorDead);
        Validate(PlayerSlot.Player2, Arena.Ghoul, Arena.Strike, creatures).Error.ShouldBe(CombatErrors.ActorStunned);
        Validate(PlayerSlot.Player1, Arena.Knight, Arena.Guard, creatures).Error.ShouldBe(CombatErrors.SpellNotKnown);
        Validate(PlayerSlot.Player2, Arena.Wraith, Arena.Guard, creatures).Error.ShouldBe(CombatErrors.NotEnoughEnergy);
    }

    [Fact]
    public void Energy_makes_a_spell_affordable()
    {
        var living = Arena.FourCreatures();
        var wraith = Arena.Find(living, Arena.Wraith);
        wraith.UnlockSpell(Arena.SpellOf(Arena.Guard));
        wraith.GainEnergy(1);

        Validate(PlayerSlot.Player2, Arena.Wraith, Arena.Guard, Arena.Snapshots(living)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_gate_lists_the_timeline_creatures_without_an_intent()
    {
        var round = Arena.CombatRoundAt(RoundSubPhase.IntentSelection);

        var open = IntentRules.Evaluate(round);
        open.CanAdvance.ShouldBeFalse();
        open.Missing.ShouldBe([Arena.Knight, Arena.Archer, Arena.Ghoul, Arena.Wraith]);

        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Arena.Knight, Arena.Strike));
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Arena.Wraith, Arena.Strike));

        var partial = IntentRules.Evaluate(round);
        partial.CanAdvance.ShouldBeFalse();
        partial.Missing.ShouldBe([Arena.Archer, Arena.Ghoul]);

        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Arena.Archer, Arena.Strike));
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Arena.Ghoul, Arena.Strike));

        var closed = IntentRules.Evaluate(round);
        closed.CanAdvance.ShouldBeTrue();
        closed.Missing.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_timeline_has_nothing_to_wait_for()
    {
        IntentRules.Evaluate(Arena.RoundAt(RoundSubPhase.IntentSelection)).CanAdvance.ShouldBeTrue();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var intent = new CombatIntent(Arena.Knight, Arena.Strike);

        Should.Throw<ArgumentNullException>(() => IntentRules.ValidateIntent(PlayerSlot.Player1, null!, creatures, Arena.Resources));
        Should.Throw<ArgumentNullException>(() => IntentRules.ValidateIntent(PlayerSlot.Player1, intent, null!, Arena.Resources));
        Should.Throw<ArgumentNullException>(() => IntentRules.ValidateIntent(PlayerSlot.Player1, intent, creatures, null!));
        Should.Throw<ArgumentNullException>(() => IntentRules.Evaluate(null!));
    }

    private static Result Validate(PlayerSlot slot, CreatureId actor, SpellId spell, IReadOnlyList<CreatureSnapshot> creatures) =>
        IntentRules.ValidateIntent(slot, new CombatIntent(actor, spell), creatures, Arena.Resources);
}
