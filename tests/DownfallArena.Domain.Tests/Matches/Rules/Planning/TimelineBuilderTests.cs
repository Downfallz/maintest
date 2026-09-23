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
    public void Quick_slots_come_first_then_initiative_descending()
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
        ], new ScriptedRolls(12, 7));

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Ghoul, Arena.Wraith, Arena.Knight, Arena.Archer]);
        timeline[0].Speed.ShouldBe(Speed.Quick);
        timeline[0].Initiative.Value.ShouldBe(5);
        timeline[2].Initiative.Value.ShouldBe(3);
        timeline[3].Speed.ShouldBe(Speed.Standard);
    }

    /// <summary>
    /// The other direction, and the reason `InitiativeBuff` exists (ADR 0036): a haste moves its creature *up*
    /// the order. All four are Quick and tie on base initiative, so nothing but the buff can separate them: the
    /// other three roll off the tie the buff lifted it out of, and nothing but the buff moved it.
    /// </summary>
    [Fact]
    public void An_initiative_buff_moves_its_creature_up_the_timeline()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Wraith).Apply(InitiativeBuff.Of(3, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);

        var timeline = TimelineBuilder.Build(creatures,
        [
            new SpeedChoice(Arena.Knight, Speed.Quick),
            new SpeedChoice(Arena.Archer, Speed.Quick),
            new SpeedChoice(Arena.Ghoul, Speed.Quick),
            new SpeedChoice(Arena.Wraith, Speed.Quick),
        ], new ScriptedRolls(19, 11, 3));

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Wraith, Arena.Knight, Arena.Archer, Arena.Ghoul]);
        timeline[0].Initiative.Value.ShouldBe(8, "the last creature by every other rule goes first on the buff alone");
    }

    [Fact]
    public void Creatures_without_a_choice_are_left_out()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var timeline = TimelineBuilder.Build(creatures, [new SpeedChoice(Arena.Archer, Speed.Standard)], new ScriptedRolls());

        timeline.Count.ShouldBe(1);
        timeline[0].Owner.ShouldBe(PlayerSlot.Player1);
    }

    [Fact]
    public void A_choice_for_an_unknown_creature_is_an_invariant_violation()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        Should.Throw<InvalidOperationException>(() => TimelineBuilder.Build(creatures, [new SpeedChoice(CreatureId.From(9), Speed.Quick)], new ScriptedRolls()));
    }

    /// <summary>
    /// ADR 0063. Under packages two sides buying the same package in the same round tie everywhere, and a tie
    /// that went to Player 1 won the greedy mirror 400 times in 400. The roll is the whole order now: here the
    /// last creature by seat rolls highest and acts first.
    /// </summary>
    [Fact]
    public void A_tie_goes_to_the_higher_d20_roll_whatever_the_seat()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var timeline = TimelineBuilder.Build(creatures, AllQuick(), new ScriptedRolls(3, 9, 15, 20));

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Wraith, Arena.Ghoul, Arena.Archer, Arena.Knight]);
    }

    [Fact]
    public void Creatures_that_roll_the_same_number_roll_again_among_themselves()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var rolls = new ScriptedRolls(10, 10, 4, 10, 2, 18, 11);

        var timeline = TimelineBuilder.Build(creatures, AllQuick(), rolls);

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Archer, Arena.Wraith, Arena.Knight, Arena.Ghoul]);
        rolls.Remaining.ShouldBe(0, "the three tens roll once more and the four does not");
    }

    [Fact]
    public void A_tie_is_rolled_on_a_d20()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        TimelineBuilder.Build(creatures, AllQuick(), new ScriptedRolls(1, 2, 3, 20))[0].Creature.ShouldBe(Arena.Wraith);
        Should.Throw<InvalidOperationException>(() => TimelineBuilder.Build(creatures, AllQuick(), new ScriptedRolls(21, 1, 2, 3)),
            "ScriptedRolls refuses a roll outside the range it is asked for, and 21 is outside a d20");
    }

    [Fact]
    public void A_creature_alone_on_its_speed_and_initiative_rolls_nothing()
    {
        var living = Arena.FourCreatures();
        Arena.Find(living, Arena.Archer).Apply(InitiativeBuff.Of(1, Duration.OfRounds(1)));
        var creatures = Arena.Snapshots(living);

        var timeline = TimelineBuilder.Build(creatures,
        [
            new SpeedChoice(Arena.Knight, Speed.Quick),
            new SpeedChoice(Arena.Archer, Speed.Quick),
            new SpeedChoice(Arena.Ghoul, Speed.Standard),
        ], new ScriptedRolls());

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Archer, Arena.Knight, Arena.Ghoul],
            "equal initiative at different speeds is not a tie, and neither is a buffed creature above one");
    }

    /// <summary>
    /// Two creatures of one side that roll the same number are not rolled again: which of them takes which of
    /// their side's places is their owner's tie order, not the dice's (ADR 0063).
    /// </summary>
    [Fact]
    public void Creatures_of_one_side_that_roll_the_same_number_are_not_rolled_again()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());
        var rolls = new ScriptedRolls(10, 10, 4);

        var timeline = TimelineBuilder.Build(creatures,
        [
            new SpeedChoice(Arena.Knight, Speed.Quick),
            new SpeedChoice(Arena.Archer, Speed.Quick),
            new SpeedChoice(Arena.Ghoul, Speed.Quick),
            new SpeedChoice(Arena.Wraith, Speed.Standard),
        ], rolls);

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Knight, Arena.Archer, Arena.Ghoul, Arena.Wraith]);
        rolls.Remaining.ShouldBe(0);
    }

    /// <summary>A tie held by one side alone is its owner's to order (ADR 0063), so no die is rolled for it.</summary>
    [Fact]
    public void A_tie_held_by_one_side_alone_rolls_nothing()
    {
        var creatures = Arena.Snapshots(Arena.FourCreatures());

        var timeline = TimelineBuilder.Build(creatures,
        [
            new SpeedChoice(Arena.Knight, Speed.Quick),
            new SpeedChoice(Arena.Archer, Speed.Quick),
            new SpeedChoice(Arena.Ghoul, Speed.Standard),
            new SpeedChoice(Arena.Wraith, Speed.Standard),
        ], new ScriptedRolls());

        timeline.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Knight, Arena.Archer, Arena.Ghoul, Arena.Wraith]);
    }

    private static SpeedChoice[] AllQuick() =>
    [
        new SpeedChoice(Arena.Knight, Speed.Quick),
        new SpeedChoice(Arena.Archer, Speed.Quick),
        new SpeedChoice(Arena.Ghoul, Speed.Quick),
        new SpeedChoice(Arena.Wraith, Speed.Quick),
    ];
}
