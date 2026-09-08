using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

public sealed class TimelineBuilderTests
{
    [Fact]
    public void Quick_slots_come_first_then_initiative_then_player_slot_then_creature_id()
    {
        var living = Arena.FourCreatures();
        living[0].Apply(InitiativeDebuff.Of(2, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);

        var timeline = TimelineBuilder.Build(creatures,
        [
            new SpeedChoice(Arena.Knight, Speed.Quick),
            new SpeedChoice(Arena.Archer, Speed.Standard),
            new SpeedChoice(Arena.Ghoul, Speed.Quick),
            new SpeedChoice(Arena.Wraith, Speed.Quick),
        ]);

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Ghoul, Arena.Wraith, Arena.Knight, Arena.Archer]);
        timeline[0].Speed.ShouldBe(Speed.Quick);
        timeline[0].Initiative.Value.ShouldBe(5);
        timeline[2].Initiative.Value.ShouldBe(3);
        timeline[3].Speed.ShouldBe(Speed.Standard);
    }

    [Fact]
    public void Creatures_without_a_choice_are_left_out()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var timeline = TimelineBuilder.Build(creatures, [new SpeedChoice(Arena.Archer, Speed.Standard)]);

        timeline.Count.ShouldBe(1);
        timeline[0].Owner.ShouldBe(PlayerSlot.Player1);
    }

    [Fact]
    public void A_choice_for_an_unknown_creature_is_an_invariant_violation()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        Should.Throw<InvalidOperationException>(() => TimelineBuilder.Build(creatures, [new SpeedChoice(CreatureId.From(9), Speed.Quick)]));
    }
}
