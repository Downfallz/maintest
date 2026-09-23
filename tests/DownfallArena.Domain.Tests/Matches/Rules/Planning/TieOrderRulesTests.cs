using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rules.Planning;

public sealed class TieOrderRulesTests
{
    private static readonly CombatTimeline Mixed = CombatTimeline.Of(
    [
        Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
        Slot(PlayerSlot.Player2, Arena.Ghoul, Speed.Quick, 5),
        Slot(PlayerSlot.Player1, Arena.Archer, Speed.Quick, 5),
        Slot(PlayerSlot.Player2, Arena.Wraith, Speed.Standard, 5),
    ]);

    [Fact]
    public void A_player_owes_an_order_only_for_a_tie_in_which_they_hold_two_places()
    {
        TieOrderRules.TiesOf(Mixed, PlayerSlot.Player1).ShouldHaveSingleItem().ShouldBe([Arena.Knight, Arena.Archer]);
        TieOrderRules.HasTieOrderToGive(Mixed, PlayerSlot.Player2).ShouldBeFalse("the Ghoul and the Wraith are in different bands");
    }

    [Fact]
    public void An_order_moves_the_player_s_creatures_between_their_own_places_and_leaves_the_other_side_where_it_is()
    {
        var reordered = TieOrderRules.Apply(Mixed, new Dictionary<PlayerSlot, IReadOnlyList<CreatureId>>
        {
            [PlayerSlot.Player1] = [Arena.Archer, Arena.Knight],
        });

        reordered.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Archer, Arena.Ghoul, Arena.Knight, Arena.Wraith]);
    }

    /// <summary>
    /// The order names the creatures of the ties the player owes, and none of a tie where they hold one place;
    /// that creature stays where the roll put it rather than being looked for in an order that never had it.
    /// </summary>
    [Fact]
    public void A_creature_alone_on_its_side_of_a_tie_keeps_its_place_when_its_owner_orders_another_tie()
    {
        var lone = CreatureId.From(3);
        var timeline = CombatTimeline.Of(
        [
            Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, Arena.Archer, Speed.Quick, 5),
            Slot(PlayerSlot.Player2, Arena.Ghoul, Speed.Standard, 5),
            Slot(PlayerSlot.Player1, lone, Speed.Standard, 5),
        ]);

        var reordered = TieOrderRules.Apply(timeline, new Dictionary<PlayerSlot, IReadOnlyList<CreatureId>>
        {
            [PlayerSlot.Player1] = [Arena.Archer, Arena.Knight],
        });

        reordered.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Archer, Arena.Knight, Arena.Ghoul, lone]);
    }

    /// <summary>One order for a player with two ties to give: each tie takes its own creatures in the order given.</summary>
    [Fact]
    public void One_order_orders_every_tie_the_player_has_each_on_its_own()
    {
        var third = CreatureId.From(3);
        var fourth = CreatureId.From(6);
        var timeline = CombatTimeline.Of(
        [
            Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, Arena.Archer, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, third, Speed.Standard, 5),
            Slot(PlayerSlot.Player2, Arena.Ghoul, Speed.Standard, 5),
            Slot(PlayerSlot.Player1, fourth, Speed.Standard, 5),
        ]);
        IReadOnlyList<CreatureId> order = [Arena.Archer, fourth, Arena.Knight, third];

        TieOrderRules.ValidateOrder(PlayerSlot.Player1, order, timeline).IsSuccess.ShouldBeTrue();
        var reordered = TieOrderRules.Apply(timeline, new Dictionary<PlayerSlot, IReadOnlyList<CreatureId>> { [PlayerSlot.Player1] = order });

        reordered.Slots.Select(slot => slot.Creature).ShouldBe([Arena.Archer, Arena.Knight, fourth, Arena.Ghoul, third]);
    }

    [Fact]
    public void An_order_that_names_a_creature_alone_on_its_side_of_a_tie_is_refused()
    {
        var lone = CreatureId.From(3);
        var timeline = CombatTimeline.Of(
        [
            Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, Arena.Archer, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, lone, Speed.Standard, 5),
            Slot(PlayerSlot.Player2, Arena.Ghoul, Speed.Standard, 5),
        ]);

        TieOrderRules.ValidateOrder(PlayerSlot.Player1, [Arena.Archer, Arena.Knight, lone], timeline).Error.ShouldBe(PlanningErrors.TieOrderMismatch);
    }

    [Fact]
    public void A_tie_order_keeps_the_rolls_that_gave_each_side_its_places()
    {
        IReadOnlyList<RollOff> rolls = [new RollOff(Arena.Knight, [17]), new RollOff(Arena.Ghoul, [9]), new RollOff(Arena.Archer, [3])];

        var reordered = TieOrderRules.Apply(Mixed.WithRollOffs(rolls), new Dictionary<PlayerSlot, IReadOnlyList<CreatureId>>
        {
            [PlayerSlot.Player1] = [Arena.Archer, Arena.Knight],
        });

        reordered.RollOffs.ShouldBe(rolls);
    }

    [Fact]
    public void A_roll_for_a_creature_that_is_not_on_the_timeline_is_refused()
    {
        Should.Throw<ArgumentException>(() => Mixed.WithRollOffs([new RollOff(CreatureId.From(9), [20])]));
    }

    [Fact]
    public void Slots_that_tie_on_initiative_at_different_speeds_are_not_one_tie()
    {
        var timeline = CombatTimeline.Of(
        [
            Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, Arena.Archer, Speed.Standard, 5),
        ]);

        TieOrderRules.HasTieOrderToGive(timeline, PlayerSlot.Player1).ShouldBeFalse();
        TieOrderRules.ValidateOrder(PlayerSlot.Player1, [Arena.Archer, Arena.Knight], timeline).Error.ShouldBe(PlanningErrors.NoTieToOrder);
    }

    private static ActivationSlot Slot(PlayerSlot owner, CreatureId creature, Speed speed, int initiative) =>
        new(owner, creature, speed, Initiative.Of(initiative));
}
