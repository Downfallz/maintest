using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Resources.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Creatures;

public sealed class ConditionTests
{
    private static Creature Spawn() => Creature.Spawn(CreatureId.From(1), PlayerSlot.Player1, Content.Creature());

    [Fact]
    public void A_stun_lasts_its_duration_then_expires()
    {
        var creature = Spawn();

        var condition = creature.Apply(Stun.For(1)).ShouldNotBeNull();

        condition.RemainingRounds.ShouldBe(1);
        creature.IsStunned.ShouldBeTrue();

        creature.TickConditions().ShouldBeEmpty();
        var expired = creature.TickConditions();

        expired.ShouldBe([condition]);
        condition.IsExpired.ShouldBeTrue();
        creature.IsStunned.ShouldBeFalse();
        creature.Conditions.ShouldBeEmpty();
    }

    [Fact]
    public void The_first_tick_after_application_does_not_count()
    {
        var creature = Spawn();
        var condition = creature.Apply(Bleed.Of(1, rounds: 2)).ShouldNotBeNull();

        creature.TickConditions();
        condition.RemainingRounds.ShouldBe(2);

        creature.TickConditions();
        condition.RemainingRounds.ShouldBe(1);
    }

    [Fact]
    public void Stacking_effects_add_up()
    {
        var creature = Spawn();

        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(2)));
        creature.Apply(DefenseBuff.Of(3, Duration.OfRounds(2)));

        creature.Conditions.Count.ShouldBe(2);
        creature.TotalDefense.ShouldBe(Defense.Of(5));
    }

    [Fact]
    public void A_second_bleed_runs_beside_the_first_instead_of_replacing_it()
    {
        var creature = Spawn();
        var first = creature.Apply(Bleed.Of(1, rounds: 2)).ShouldNotBeNull();
        creature.TickConditions();
        creature.TickConditions();
        first.RemainingRounds.ShouldBe(1);

        var second = creature.Apply(Bleed.Of(5, rounds: 2)).ShouldNotBeNull();

        second.ShouldNotBeSameAs(first);
        creature.Conditions.Count.ShouldBe(2);
        first.RemainingRounds.ShouldBe(1);
        second.RemainingRounds.ShouldBe(2);
    }

    [Fact]
    public void Refreshing_effects_restart_the_existing_duration()
    {
        var creature = Spawn();
        var first = creature.Apply(Stun.For(2)).ShouldNotBeNull();
        creature.TickConditions();
        creature.TickConditions();
        first.RemainingRounds.ShouldBe(1);

        var refreshed = creature.Apply(Stun.For(2));

        refreshed.ShouldBeSameAs(first);
        creature.Conditions.ShouldHaveSingleItem().RemainingRounds.ShouldBe(2);
        creature.TickConditions();
        first.RemainingRounds.ShouldBe(2);
    }

    [Fact]
    public void Ignored_effects_do_not_apply_while_one_is_active()
    {
        var creature = Spawn();
        creature.Apply(DefenseBuff.Of(2, Duration.OfRounds(1), StackingPolicy.Ignore)).ShouldNotBeNull();

        creature.Apply(DefenseBuff.Of(9, Duration.OfRounds(1), StackingPolicy.Ignore)).ShouldBeNull();

        creature.TotalDefense.ShouldBe(Defense.Of(2));
        creature.TickConditions();
        creature.TickConditions();
        creature.Apply(DefenseBuff.Of(9, Duration.OfRounds(1), StackingPolicy.Ignore)).ShouldNotBeNull();
        creature.TotalDefense.ShouldBe(Defense.Of(9));
    }

    [Fact]
    public void Permanent_conditions_never_expire()
    {
        var creature = Spawn();
        var condition = creature.Apply(DefenseBuff.Of(1, Duration.Permanent)).ShouldNotBeNull();

        creature.TickConditions().ShouldBeEmpty();
        creature.TickConditions().ShouldBeEmpty();

        condition.IsPermanent.ShouldBeTrue();
        condition.RemainingRounds.ShouldBeNull();
        creature.TotalDefense.ShouldBe(Defense.Of(1));
    }

    [Fact]
    public void Initiative_debuffs_lower_the_current_initiative_down_to_zero()
    {
        var creature = Spawn();

        creature.Apply(InitiativeDebuff.Of(2, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(3));

        creature.Apply(InitiativeDebuff.Of(10, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(0));
    }

    /// <summary>
    /// The buff is the debuff's mirror and the two meet in one total (ADR 0036). The order matters: the buffs
    /// are added before the debuffs are taken off, so a creature buffed past what a debuff takes keeps the
    /// difference instead of losing it to a floor applied halfway through.
    /// </summary>
    [Fact]
    public void Initiative_buffs_raise_the_current_initiative_and_meet_the_debuffs_in_one_total()
    {
        var creature = Spawn();

        creature.Apply(InitiativeBuff.Of(3, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(8));

        creature.Apply(InitiativeDebuff.Of(10, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(0), "the total floors at zero, not each half");

        creature.Apply(InitiativeBuff.Of(4, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(2), "5 + 3 + 4 - 10, and not zero plus four");
    }

    /// <summary>A buff that runs out gives the speed back, the same as a debuff that runs out takes it back.</summary>
    [Fact]
    public void An_initiative_buff_lasts_the_rounds_it_was_given()
    {
        var creature = Spawn();
        creature.Apply(InitiativeBuff.Of(2, Duration.OfRounds(1)));
        creature.CurrentInitiative.ShouldBe(Initiative.Of(7));

        creature.TickConditions();
        creature.TickConditions();

        creature.CurrentInitiative.ShouldBe(Initiative.Of(5));
    }

    /// <summary>
    /// A defense debuff is the buff's mirror and the two meet in the same total (ADR 0035): they add up
    /// against each other, and the total floors at zero rather than turning damage into a bonus.
    /// </summary>
    [Fact]
    public void Defense_debuffs_lower_the_total_defense_down_to_zero()
    {
        var creature = Spawn();

        creature.Apply(DefenseBuff.Of(4, Duration.OfRounds(2)));
        creature.Apply(DefenseDebuff.Of(3, Duration.OfRounds(1)));
        creature.TotalDefense.ShouldBe(Defense.Of(1));

        creature.Apply(DefenseDebuff.Of(9, Duration.Permanent));
        creature.TotalDefense.ShouldBe(Defense.Of(0));
    }

    /// <summary>A defense debuff that runs out gives the defense back; a permanent one never does.</summary>
    [Fact]
    public void A_defense_debuff_lasts_the_rounds_it_was_given()
    {
        var creature = Spawn();
        creature.Apply(DefenseBuff.Of(5, Duration.Permanent));

        creature.Apply(DefenseDebuff.Of(2, Duration.OfRounds(1)));
        creature.TotalDefense.ShouldBe(Defense.Of(3));

        creature.TickConditions();
        creature.TickConditions();

        creature.TotalDefense.ShouldBe(Defense.Of(5));
    }

    [Fact]
    public void A_dead_creature_takes_no_conditions_and_is_not_stunned()
    {
        var creature = Spawn();
        creature.Apply(Stun.For(3));
        creature.TakeDamage(20);

        creature.Apply(Stun.For(3)).ShouldBeNull();
        creature.IsStunned.ShouldBeFalse();
    }

    [Fact]
    public void Snapshots_describe_the_conditions()
    {
        var creature = Spawn();
        creature.Apply(Bleed.Of(1, rounds: 3));

        var snapshot = creature.Snapshot();

        snapshot.Conditions.ShouldBe([new ConditionSnapshot(Bleed.Of(1, rounds: 3), 3)]);
        snapshot.TotalDefense.ShouldBe(Defense.Of(0));
    }
}
