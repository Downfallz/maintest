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
    public void Refreshing_effects_restart_the_existing_duration_and_keep_its_amount()
    {
        var creature = Spawn();
        var first = creature.Apply(Bleed.Of(1, rounds: 2)).ShouldNotBeNull();
        creature.TickConditions();
        creature.TickConditions();
        first.RemainingRounds.ShouldBe(1);

        var refreshed = creature.Apply(Bleed.Of(5, rounds: 2));

        refreshed.ShouldBeSameAs(first);
        creature.Conditions.ShouldHaveSingleItem().RemainingRounds.ShouldBe(2);
        ((Bleed)creature.Conditions[0].Effect).AmountPerRound.ShouldBe(1);
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
