using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rounds;

public sealed class CombatTimelineTests
{
    private static readonly ActivationSlot First = new(PlayerSlot.Player1, CreatureId.From(1), Speed.Quick, Initiative.Of(5));
    private static readonly ActivationSlot Second = new(PlayerSlot.Player2, CreatureId.From(4), Speed.Standard, Initiative.Of(3));

    [Fact]
    public void A_timeline_keeps_its_slots_in_order()
    {
        var timeline = CombatTimeline.Of([First, Second]);

        timeline.Count.ShouldBe(2);
        timeline[0].ShouldBe(First);
        timeline[1].ShouldBe(Second);
        timeline.Slots.ShouldBe([First, Second]);
        timeline.SlotOf(CreatureId.From(4)).ShouldBe(Second);
        timeline.SlotOf(CreatureId.From(9)).ShouldBeNull();
    }

    [Fact]
    public void The_empty_timeline_has_no_slot()
    {
        CombatTimeline.Empty.Count.ShouldBe(0);
        CombatTimeline.Empty.Slots.ShouldBeEmpty();
    }

    [Fact]
    public void A_creature_cannot_appear_twice()
    {
        Should.Throw<ArgumentException>(() => CombatTimeline.Of([First, First with { Speed = Speed.Standard }]));
    }

    [Fact]
    public void An_action_binds_an_intent_to_copied_targets_and_compares_by_value()
    {
        var intent = new CombatIntent(CreatureId.From(1), SpellId.Parse("spell:strike:v1"));
        var targets = new List<CreatureId> { CreatureId.From(4) };

        var action = CombatAction.Bind(intent, targets);
        targets.Add(CreatureId.From(5));

        action.Actor.ShouldBe(CreatureId.From(1));
        action.Spell.ShouldBe(SpellId.Parse("spell:strike:v1"));
        action.Targets.ShouldBe([CreatureId.From(4)]);
        action.ShouldBe(CombatAction.Bind(intent, [CreatureId.From(4)]));
        action.ShouldNotBe(CombatAction.Bind(intent, [CreatureId.From(5)]));
        action.GetHashCode().ShouldBe(CombatAction.Bind(intent, [CreatureId.From(4)]).GetHashCode());
    }
}
