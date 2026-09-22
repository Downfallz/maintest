using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.Domain.Tests.Matches.Support;
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
        creature.BuyTier(Package("tier:new:v1")).Error.ShouldBe(CreatureErrors.Dead);
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

    /// <summary>
    /// Learning is idempotent rather than refusable, because two packages may teach the same spell and the
    /// second purchase is not a mistake (ADR 0056).
    /// </summary>
    [Fact]
    public void Learning_a_spell_twice_leaves_one_of_it()
    {
        var creature = Spawn();
        var guard = Content.Spell("spell:guard:v1");

        creature.Learn(guard.Id);
        creature.Learn(guard.Id);

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
    public void Buying_two_packages_raises_the_base_initiative_by_both()
    {
        var creature = Spawn();

        creature.BuyTier(Package("tier:first:v1", bonus: 2)).IsSuccess.ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(7));

        creature.BuyTier(Package("tier:second:v1", bonus: 3, spells: ["spell:slam:v1"])).IsSuccess.ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(10));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(10));
    }

    /// <summary>
    /// A spell is worth no initiative on its own any more: the bonus belongs to the package that teaches it
    /// and is paid when the package is bought (ADR 0056). This is what stops a creature taught a spell by two
    /// packages from being paid twice for it.
    /// </summary>
    [Fact]
    public void Learning_a_spell_raises_no_initiative()
    {
        var creature = Spawn();

        creature.Learn(SpellId.Parse("spell:guard:v1"));

        creature.KnowsSpell(SpellId.Parse("spell:guard:v1")).ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(Initiative.Of(5));
    }

    [Fact]
    public void An_initiative_debuff_lowers_the_current_initiative_and_leaves_the_base_alone()
    {
        var creature = Spawn();
        creature.BuyTier(Package("tier:guard:v1", bonus: 3));

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
        creature.BuyTier(Package("tier:guard:v1", bonus: 3));

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
        creature.BuyTier(Package("tier:guard:v1", bonus: 2)).IsSuccess.ShouldBeTrue();
        creature.Apply(Stun.For(2));
        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(1)), new ConditionSource(CreatureId.From(3), SpellId.Parse("spell:guard:v1")));
        var snapshot = creature.Snapshot();

        var restored = Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot));

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
        var restored = Creature.Restore(creature.Snapshot(), Content.Creature(), Arena.TiersOf(creature.Snapshot()));

        restored.TakeDamage(5);
        restored.Apply(Stun.For(1));

        creature.Health.ShouldBe(Health.Of(20));
        creature.IsStunned.ShouldBeFalse();
    }

    [Fact]
    public void A_creature_cannot_be_restored_from_another_definitions_snapshot()
    {
        var snapshot = Spawn().Snapshot();

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature("creature:other:v1"), Arena.TiersOf(snapshot)));
    }

    [Fact]
    public void A_creature_cannot_be_restored_with_a_defense_its_conditions_do_not_give()
    {
        var creature = Spawn();
        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(1)));
        var snapshot = creature.Snapshot() with { TotalDefense = Defense.Of(0) };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    [Fact]
    public void A_creature_cannot_be_restored_stunned_without_a_stun()
    {
        var snapshot = Spawn().Snapshot() with { IsStunned = true };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    [Fact]
    public void A_creature_cannot_be_restored_with_an_initiative_its_conditions_do_not_give()
    {
        var snapshot = Spawn().Snapshot() with { CurrentInitiative = Initiative.Of(9) };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    [Fact]
    public void A_creature_cannot_be_restored_knowing_a_spell_no_package_of_its_taught_it()
    {
        var snapshot = Spawn().Snapshot() with { KnownSpells = new HashSet<SpellId> { SpellId.Parse("spell:strike:v1"), SpellId.Parse("spell:forbidden:v1") } };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    [Fact]
    public void A_creature_cannot_be_restored_without_its_starting_spells()
    {
        var snapshot = Spawn().Snapshot() with { KnownSpells = new HashSet<SpellId> { SpellId.Parse("spell:guard:v1") } };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    /// <summary>
    /// The packages handed in have to be the ones the snapshot says it owns. A caller that resolved the wrong
    /// set would restore a creature whose spells are explained by packages it never bought.
    /// </summary>
    [Fact]
    public void A_creature_cannot_be_restored_with_packages_it_does_not_own()
    {
        var snapshot = Spawn().Snapshot();

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), [Package("tier:unbought:v1")]));
    }

    [Fact]
    public void A_creature_cannot_be_restored_above_its_maximum_health()
    {
        var snapshot = Spawn().Snapshot() with { Health = Health.Of(21) };

        Should.Throw<ArgumentException>(() => Creature.Restore(snapshot, Content.Creature(), Arena.TiersOf(snapshot)));
    }

    private static Tier Package(string id, int level = 1, int bonus = 2, string[]? requires = null, string[]? spells = null) =>
        Tier.Create(
            TierId.Parse(id),
            id,
            level,
            [.. (requires ?? []).Select(TierId.Parse)],
            [.. (spells ?? ["spell:guard:v1"]).Select(SpellId.Parse)],
            Initiative.Of(bonus));

    [Fact]
    public void Buying_a_package_teaches_every_spell_in_it_and_raises_initiative_once()
    {
        var creature = Spawn();
        var before = creature.BaseInitiative;

        creature.BuyTier(Package("tier:brute:v1", bonus: 2, spells: ["spell:guard:v1", "spell:slam:v1"])).IsSuccess.ShouldBeTrue();

        creature.OwnsTier(TierId.Parse("tier:brute:v1")).ShouldBeTrue();
        creature.KnowsSpell(SpellId.Parse("spell:guard:v1")).ShouldBeTrue();
        creature.KnowsSpell(SpellId.Parse("spell:slam:v1")).ShouldBeTrue();
        creature.BaseInitiative.ShouldBe(before.Plus(2));
    }

    [Fact]
    public void A_package_cannot_be_bought_twice()
    {
        var creature = Spawn();
        creature.BuyTier(Package("tier:brute:v1")).IsSuccess.ShouldBeTrue();
        var after = creature.BaseInitiative;

        creature.BuyTier(Package("tier:brute:v1")).Error.ShouldBe(CreatureErrors.TierAlreadyOwned);

        creature.BaseInitiative.ShouldBe(after);
    }

    /// <summary>
    /// Ownership is asked of the tier, never of its spells: knowing what a package teaches is not having
    /// bought it, and a starting kit can hand over a spell a package also teaches.
    /// </summary>
    [Fact]
    public void Knowing_a_packages_spells_is_not_owning_the_package()
    {
        var creature = Spawn();
        creature.BuyTier(Package("tier:brute:v1", spells: ["spell:guard:v1"])).IsSuccess.ShouldBeTrue();

        creature.KnowsSpell(SpellId.Parse("spell:guard:v1")).ShouldBeTrue();
        creature.OwnsTier(TierId.Parse("tier:marauder:v1")).ShouldBeFalse();
    }

    /// <summary>A spell two packages teach is granted again without refusing the second purchase.</summary>
    [Fact]
    public void A_package_grants_a_spell_the_creature_already_knows_without_refusing()
    {
        var creature = Spawn();
        creature.BuyTier(Package("tier:brute:v1", spells: ["spell:guard:v1"])).IsSuccess.ShouldBeTrue();

        creature.BuyTier(Package("tier:prowler:v1", spells: ["spell:guard:v1", "spell:slam:v1"])).IsSuccess.ShouldBeTrue();

        creature.KnowsSpell(SpellId.Parse("spell:guard:v1")).ShouldBeTrue();
        creature.AcquiredTiers.Count.ShouldBe(2);
    }

    [Fact]
    public void A_package_whose_prerequisite_is_not_owned_is_refused_and_changes_nothing()
    {
        var creature = Spawn();
        var before = creature.BaseInitiative;

        creature.BuyTier(Package("tier:marauder:v1", level: 2, requires: ["tier:brute:v1"], spells: ["spell:slam:v1"]))
            .Error.ShouldBe(CreatureErrors.TierPrerequisiteMissing);

        creature.AcquiredTiers.ShouldBeEmpty();
        creature.KnowsSpell(SpellId.Parse("spell:slam:v1")).ShouldBeFalse();
        creature.BaseInitiative.ShouldBe(before);
    }

    [Fact]
    public void A_dead_creature_buys_nothing()
    {
        var creature = Spawn();
        creature.TakeDamage(999);

        creature.BuyTier(Package("tier:brute:v1")).Error.ShouldBe(CreatureErrors.Dead);

        creature.AcquiredTiers.ShouldBeEmpty();
    }

    /// <summary>
    /// The snapshot's base initiative already carries every bonus the creature bought, so restoring the list
    /// of packages must not add them a second time. Getting this wrong makes a creature faster every time a
    /// rollout reconstructs it, which is the kind of drift a search would silently learn to exploit.
    /// </summary>
    [Fact]
    public void Restoring_a_creature_does_not_pay_its_package_bonuses_again()
    {
        var creature = Spawn();
        var brute = Package("tier:brute:v1", bonus: 3);
        creature.BuyTier(brute).IsSuccess.ShouldBeTrue();
        var snapshot = creature.Snapshot();

        var restored = Creature.Restore(snapshot, Content.Creature(), [brute]);

        restored.BaseInitiative.ShouldBe(snapshot.BaseInitiative);
        restored.AcquiredTiers.ShouldBe(creature.AcquiredTiers);
        restored.Snapshot().BaseInitiative.ShouldBe(snapshot.BaseInitiative);
    }

    /// <summary>
    /// What a creature knows and what it owns are handed out read-only in the strong sense. Ownership is held
    /// rather than inferred precisely so it cannot drift from the spells and the initiative that were paid for
    /// it; a caller that reached back through the returned set and granted a package would produce that drift
    /// without going through <see cref="Creature.BuyTier"/>.
    /// </summary>
    [Fact]
    public void What_a_creature_knows_and_owns_cannot_be_changed_through_the_sets_it_hands_out()
    {
        var creature = Spawn();
        creature.BuyTier(Package("tier:brute:v1", spells: ["spell:guard:v1"])).IsSuccess.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => ((ISet<TierId>)creature.AcquiredTiers).Add(TierId.Parse("tier:marauder:v1")));
        Should.Throw<NotSupportedException>(() => ((ISet<SpellId>)creature.KnownSpells).Clear());

        creature.AcquiredTiers.ShouldBe([TierId.Parse("tier:brute:v1")]);
        creature.KnowsSpell(SpellId.Parse("spell:guard:v1")).ShouldBeTrue();
    }
}
