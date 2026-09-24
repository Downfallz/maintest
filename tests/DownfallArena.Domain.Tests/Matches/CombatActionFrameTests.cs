using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class CombatActionFrameTests
{
    [Fact]
    public void Frames_preserve_action_boundaries_before_cleanup_and_upkeep()
    {
        var match = Table.Started();
        var creature = Table.CreatureNumber(match, 1);
        creature.Apply(Bleed.Of(1, rounds: 1));
        Table.PlayRound(match);

        var events = match.DomainEvents.OfType<CombatActionResolved>().ToList();
        var first = events[0].Frame.ShouldNotBeNull();
        var last = events[^1].Frame.ShouldNotBeNull();
        first.Before.Single(one => one.Id == creature.Id).Health.ShouldBe(Health.Of(20));
        first.After.Single(one => one.Id.Value == 3).Health.ShouldBe(Health.Of(17));
        first.Timeline.Select(slot => slot.Creature.Value).ShouldBe([1, 2, 3, 4]);
        first.RollOffs.ShouldBe(match.DomainEvents.OfType<CombatActionResolved>().Last().Frame!.RollOffs);
        var atBoundary = last.After.Single(one => one.Id == creature.Id);
        atBoundary.Health.ShouldBe(Health.Of(14));
        atBoundary.Energy.ShouldBe(Energy.Of(2));
        atBoundary.Conditions.ShouldHaveSingleItem().IsFresh.ShouldBeTrue();
        creature.Health.ShouldBe(Health.Of(13));
        creature.Energy.ShouldBe(Energy.Of(4));
        creature.Snapshot().Conditions.ShouldHaveSingleItem().IsFresh.ShouldBeFalse();

        Table.PlayRound(match);

        atBoundary.Health.ShouldBe(Health.Of(14));
        atBoundary.Conditions.ShouldHaveSingleItem().IsFresh.ShouldBeTrue();
        first.Before.Single(one => one.Id == creature.Id).Health.ShouldBe(Health.Of(20));
    }

    [Fact]
    public void Lethal_damage_and_fizzles_keep_the_actual_states()
    {
        var match = Table.Started();
        for (var round = 0; round < 4; round++)
        {
            Table.PlayRound(match);
        }

        var events = match.DomainEvents.OfType<CombatActionResolved>().Where(action => action.RoundId.Number == 4).ToList();
        var lethal = events[0].Frame.ShouldNotBeNull();
        lethal.Before.Single(one => one.Id.Value == 3).Health.ShouldBe(Health.Of(2));
        lethal.After.Single(one => one.Id.Value == 3).IsDead.ShouldBeTrue();
        foreach (var fizzled in events.Where(action => action.Resolution.Fizzled))
        {
            var frame = fizzled.Frame.ShouldNotBeNull();
            frame.Before.Select(one => (one.Id, one.Health, one.Energy)).ShouldBe(frame.After.Select(one => (one.Id, one.Health, one.Energy)));
            fizzled.AppliedOutcomes.ShouldBeEmpty();
        }
    }
}
