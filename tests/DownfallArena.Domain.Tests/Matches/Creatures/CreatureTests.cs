using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Creatures;

public sealed class CreatureTests
{
    private static Creature Spawn() => Creature.Spawn(CreatureId.From(1), PlayerSlot.Player1, Content.Creature());

    [Fact]
    public void A_spawned_creature_starts_with_its_definition_stats_and_spells()
    {
        var creature = Spawn();

        creature.Id.ShouldBe(CreatureId.From(1));
        creature.Owner.ShouldBe(PlayerSlot.Player1);
        creature.Name.ShouldBe("Main");
        creature.Health.ShouldBe(Health.Of(20));
        creature.MaxHealth.ShouldBe(Health.Of(20));
        creature.Energy.ShouldBe(Energy.Of(0));
        creature.TotalDefense.ShouldBe(Defense.Of(0));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(5));
        creature.CriticalChance.ShouldBe(CriticalChance.Of(0.05));
        creature.KnownSpells.ShouldBe([SpellId.Parse("spell:strike:v1")]);
        creature.IsAlive.ShouldBeTrue();
        creature.IsStunned.ShouldBeFalse();
        creature.Conditions.ShouldBeEmpty();
    }

    [Fact]
    public void Damage_reduces_health_floors_at_zero_and_kills()
    {
        var creature = Spawn();

        creature.TakeDamage(5).ShouldBe(5);
        creature.Health.ShouldBe(Health.Of(15));

        creature.TakeDamage(50).ShouldBe(15);
        creature.Health.ShouldBe(Health.Of(0));
        creature.IsDead.ShouldBeTrue();
    }

    [Fact]
    public void A_dead_creature_takes_no_damage_and_cannot_be_healed_or_energised()
    {
        var creature = Spawn();
        creature.TakeDamage(20);

        creature.TakeDamage(3).ShouldBe(0);
        creature.Heal(3).ShouldBe(0);
        creature.GainEnergy(3).ShouldBe(0);
        creature.LoseEnergy(3).ShouldBe(0);
        creature.SpendEnergy(Energy.Of(0)).Error.ShouldBe(CreatureErrors.Dead);
        creature.UnlockSpell(Content.Spell("spell:new:v1")).Error.ShouldBe(CreatureErrors.Dead);
        creature.Health.ShouldBe(Health.Of(0));
        creature.Energy.ShouldBe(Energy.Of(0));
    }

    [Fact]
    public void Healing_is_capped_at_the_maximum_health()
    {
        var creature = Spawn();
        creature.TakeDamage(6);

        creature.Heal(4).ShouldBe(4);
        creature.Heal(10).ShouldBe(2);
        creature.Health.ShouldBe(Health.Of(20));
    }

    [Fact]
    public void Energy_is_gained_and_spent_only_when_affordable()
    {
        var creature = Spawn();

        creature.GainEnergy(3).ShouldBe(3);
        creature.SpendEnergy(Energy.Of(4)).Error.ShouldBe(CreatureErrors.NotEnoughEnergy);
        creature.SpendEnergy(Energy.Of(2)).IsSuccess.ShouldBeTrue();
        creature.Energy.ShouldBe(Energy.Of(1));
    }

    [Fact]
    public void Drained_energy_is_capped_at_what_the_creature_has()
    {
        var creature = Spawn();
        creature.GainEnergy(3);

        creature.LoseEnergy(2).ShouldBe(2);
        creature.Energy.ShouldBe(Energy.Of(1));
        creature.LoseEnergy(5).ShouldBe(1);
        creature.Energy.ShouldBe(Energy.Of(0));
    }

    [Fact]
    public void Negative_amounts_are_programming_errors()
    {
        var creature = Spawn();

        Should.Throw<ArgumentOutOfRangeException>(() => creature.TakeDamage(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => creature.Heal(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => creature.GainEnergy(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => creature.LoseEnergy(-1));
    }

    [Fact]
    public void Unlocking_a_spell_adds_it_once()
    {
        var creature = Spawn();
        var guard = Content.Spell("spell:guard:v1");

        creature.UnlockSpell(guard).IsSuccess.ShouldBeTrue();
        creature.UnlockSpell(guard).Error.ShouldBe(CreatureErrors.SpellAlreadyKnown);

        creature.KnowsSpell(guard.Id).ShouldBeTrue();
        creature.KnownSpells.Count.ShouldBe(2);
    }

    [Fact]
    public void A_spawned_creature_takes_its_base_initiative_from_its_definition()
    {
        var creature = Spawn();

        creature.BaseInitiative.ShouldBe(Initiative.Of(5));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(5));
    }

    [Fact]
    public void Unlocking_a_spell_raises_the_base_initiative_by_the_spell_initiative()
    {
        var creature = Spawn();

        creature.UnlockSpell(Content.SpellAtInitiative("spell:guard:v1", 2)).IsSuccess.ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(7));

        creature.UnlockSpell(Content.SpellAtInitiative("spell:slam:v1", 3)).IsSuccess.ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(10));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(10));
    }

    /// <summary>
    /// ADR 0017: a definition's baseInitiative is authored knowing its starting kit, so the kit does not pay
    /// again. Only an unlock taken during the match raises the base.
    /// </summary>
    [Fact]
    public void A_spell_the_creature_starts_with_raises_no_initiative()
    {
        var guard = Content.SpellAtInitiative("spell:guard:v1", 2);
        var creature = Creature.Spawn(CreatureId.From(1), PlayerSlot.Player1, Content.Creature("creature:main:v1", "spell:guard:v1"));

        creature.KnowsSpell(guard.Id).ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(5));

        creature.UnlockSpell(guard).Error.ShouldBe(CreatureErrors.SpellAlreadyKnown);
        creature.BaseInitiative.ShouldBe(Initiative.Of(5));
    }

    [Fact]
    public void A_refused_unlock_raises_no_initiative()
    {
        var creature = Spawn();
        var guard = Content.SpellAtInitiative("spell:guard:v1", 2);

        creature.UnlockSpell(guard);
        creature.UnlockSpell(guard).Error.ShouldBe(CreatureErrors.SpellAlreadyKnown);

        creature.BaseInitiative.ShouldBe(Initiative.Of(7));
    }

    [Fact]
    public void A_dead_creature_unlocks_nothing_and_keeps_the_base_initiative_it_had()
    {
        var creature = Spawn();
        creature.UnlockSpell(Content.SpellAtInitiative("spell:guard:v1", 2));
        creature.TakeDamage(20);

        creature.UnlockSpell(Content.SpellAtInitiative("spell:slam:v1", 3)).Error.ShouldBe(CreatureErrors.Dead);
        creature.BaseInitiative.ShouldBe(Initiative.Of(7));
    }

    [Fact]
    public void An_initiative_debuff_lowers_the_current_initiative_and_leaves_the_base_alone()
    {
        var creature = Spawn();
        creature.UnlockSpell(Content.SpellAtInitiative("spell:guard:v1", 3));

        creature.Apply(InitiativeDebuff.Of(2, Duration.OfRounds(1)));

        creature.BaseInitiative.ShouldBe(Initiative.Of(8));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(6));

        var snapshot = creature.Snapshot();
        snapshot.BaseInitiative.ShouldBe(Initiative.Of(8));
        snapshot.CurrentInitiative.ShouldBe(Initiative.Of(6));
    }

    /// <summary>
    /// Initiative floors at zero, so a creature debuffed past its base reads 0 current on a base that still
    /// says what it unlocked. That is the one case where the base cannot be read back from the current one,
    /// which is why the raise is recorded rather than derived (ADR 0017).
    /// </summary>
    [Fact]
    public void Debuffs_past_the_base_floor_the_current_initiative_at_zero()
    {
        var creature = Spawn();
        creature.UnlockSpell(Content.SpellAtInitiative("spell:guard:v1", 3));

        creature.Apply(InitiativeDebuff.Of(5, Duration.OfRounds(1)));
        creature.Apply(InitiativeDebuff.Of(5, Duration.OfRounds(1)));

        creature.BaseInitiative.ShouldBe(Initiative.Of(8));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(0));
    }

    [Fact]
    public void A_snapshot_copies_the_current_state()
    {
        var creature = Spawn();
        creature.TakeDamage(4);
        creature.GainEnergy(2);

        var snapshot = creature.Snapshot();
        creature.TakeDamage(1);

        snapshot.Id.ShouldBe(creature.Id);
        snapshot.Owner.ShouldBe(PlayerSlot.Player1);
        snapshot.DefinitionId.ShouldBe(creature.Definition.Id);
        snapshot.Name.ShouldBe("Main");
        snapshot.Health.ShouldBe(Health.Of(16));
        snapshot.MaxHealth.ShouldBe(Health.Of(20));
        snapshot.Energy.ShouldBe(Energy.Of(2));
        snapshot.IsAlive.ShouldBeTrue();
        snapshot.IsStunned.ShouldBeFalse();
        snapshot.KnowsSpell(SpellId.Parse("spell:strike:v1")).ShouldBeTrue();
        snapshot.Conditions.ShouldBeEmpty();
    }

    [Fact]
    public void A_restored_creature_is_the_one_its_snapshot_was_taken_of()
    {
        var creature = Spawn();
        creature.TakeDamage(4);
        creature.GainEnergy(3);
        creature.UnlockSpell(Content.SpellAtInitiative("spell:guard:v1", 2)).IsSuccess.ShouldBeTrue();
        creature.Apply(Stun.For(2));
        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(1)), new ConditionSource(CreatureId.From(3), SpellId.Parse("spell:guard:v1")));
        var snapshot = creature.Snapshot();

        var restored = Creature.Restore(snapshot, Content.Creature());

        restored.Id.ShouldBe(creature.Id);
        restored.Owner.ShouldBe(creature.Owner);
        restored.Definition.Id.ShouldBe(creature.Definition.Id);
        restored.Health.ShouldBe(Health.Of(16));
        restored.Energy.ShouldBe(Energy.Of(3));
        restored.BaseInitiative.ShouldBe(Initiative.Of(7));
        restored.KnownSpells.ShouldBe(creature.KnownSpells, ignoreOrder: true);
        restored.IsStunned.ShouldBeTrue();
        restored.TotalDefense.ShouldBe(Defense.Of(2));
        restored.Snapshot().Conditions.ShouldBe(snapshot.Conditions);
    }

    [Fact]
    public void A_restored_creature_changes_on_its_own_and_not_the_original()
    {
        var creature = Spawn();
        var restored = Creature.Restore(creature.Snapshot(), Content.Creature());

        restored.TakeDamage(5);
        restored.Apply(Stun.For(1));

        creature.Health.ShouldBe(Health.Of(20));
        creature.IsStunned.ShouldBeFalse();
    }

    [Fact]
    public void A_creature_cannot_be_restored_from_another_definitions_snapshot()
    {
        var snapshot = Spawn().Snapshot();

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature("creature:other:v1")));
    }

    [Fact]
    public void A_creature_cannot_be_restored_from_a_snapshot_that_disagrees_with_its_conditions()
    {
        var creature = Spawn();
        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(1)));
        var snapshot = creature.Snapshot();

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot with { TotalDefense = Defense.Of(0) }, Content.Creature()));
        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot with { IsStunned = true }, Content.Creature()));
        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot with { CurrentInitiative = Initiative.Of(9) }, Content.Creature()));
    }

    [Fact]
    public void A_creature_cannot_be_restored_above_its_maximum_health()
    {
        var snapshot = Spawn().Snapshot() with { Health = Health.Of(21) };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature()));
    }
}
