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

    /// <summary>
    /// ADR 0041. Two bleeds are two conditions, each with its own amount, duration and source, so a second
    /// cast adds to what the first is still doing instead of replacing it. Before that ADR this was one
    /// condition dealing 1 a round: a second attacker's bleed was the first one's refresh.
    /// </summary>
    [Fact]
    public void A_second_bleed_stacks_beside_the_first_instead_of_refreshing_it()
    {
        var creature = Spawn();

        var first = creature.Apply(Bleed.Of(1, rounds: 3)).ShouldNotBeNull();
        var second = creature.Apply(Bleed.Of(4, rounds: 2)).ShouldNotBeNull();

        second.ShouldNotBeSameAs(first);
        creature.Conditions.Count.ShouldBe(2);
        creature.Conditions.Sum(condition => ((Bleed)condition.Effect).AmountPerRound).ShouldBe(5);
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

    /// <summary>
    /// Asked for explicitly: since ADR 0041 a Bleed stacks unless the content says otherwise, so a test of
    /// what Refresh does has to name the policy. Stun is the kind that refreshes by default, but it carries
    /// no amount, and keeping the first application's amount is half of what this pins down.
    /// </summary>
    [Fact]
    public void Refreshing_effects_restart_the_existing_duration_and_keep_its_amount()
    {
        var creature = Spawn();
        var first = creature.Apply(Bleed.Of(1, rounds: 2, StackingPolicy.Refresh)).ShouldNotBeNull();
        creature.TickConditions();
        creature.TickConditions();
        first.RemainingRounds.ShouldBe(1);

        var refreshed = creature.Apply(Bleed.Of(5, rounds: 2, StackingPolicy.Refresh));

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

        snapshot.Conditions.ShouldBe([new ConditionSnapshot(Bleed.Of(1, rounds: 3), 3, IsFresh: true)]);
        snapshot.TotalDefense.ShouldBe(Defense.Of(0));
    }

    [Fact]
    public void A_snapshot_says_whether_the_first_countdown_is_still_ahead()
    {
        var creature = Spawn();
        creature.Apply(Stun.For(1));

        creature.Snapshot().Conditions.ShouldBe([new ConditionSnapshot(Stun.For(1), 1, IsFresh: true)]);
        creature.TickConditions();
        creature.Snapshot().Conditions.ShouldBe([new ConditionSnapshot(Stun.For(1), 1)]);
    }

    [Fact]
    public void A_restored_fresh_condition_still_skips_its_first_countdown()
    {
        var creature = Spawn();
        creature.Apply(Stun.For(1));

        var restored = Creature.Restore(creature.Snapshot(), Content.Creature());

        restored.TickConditions().ShouldBeEmpty();
        restored.IsStunned.ShouldBeTrue();
        restored.TickConditions().Count.ShouldBe(1);
        restored.IsStunned.ShouldBeFalse();
    }

    [Fact]
    public void A_restored_condition_past_its_first_countdown_expires_at_the_next()
    {
        var creature = Spawn();
        creature.Apply(Stun.For(1));
        creature.TickConditions();

        var restored = Creature.Restore(creature.Snapshot(), Content.Creature());

        restored.TickConditions().Count.ShouldBe(1);
        restored.IsStunned.ShouldBeFalse();
    }
}
