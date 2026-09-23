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
        TieOrderRules.GroupsOf(Mixed, PlayerSlot.Player1).ShouldHaveSingleItem().ShouldBe([Arena.Knight, Arena.Archer]);
        TieOrderRules.Owes(Mixed, PlayerSlot.Player2).ShouldBeFalse("the Ghoul and the Wraith are in different bands");
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

    [Fact]
    public void Slots_that_tie_on_initiative_at_different_speeds_are_not_one_tie()
    {
        var timeline = CombatTimeline.Of(
        [
            Slot(PlayerSlot.Player1, Arena.Knight, Speed.Quick, 5),
            Slot(PlayerSlot.Player1, Arena.Archer, Speed.Standard, 5),
        ]);

        TieOrderRules.Owes(timeline, PlayerSlot.Player1).ShouldBeFalse();
        TieOrderRules.ValidateOrder(PlayerSlot.Player1, [Arena.Archer, Arena.Knight], timeline).Error.ShouldBe(PlanningErrors.NoTieToOrder);
    }

    private static ActivationSlot Slot(PlayerSlot owner, CreatureId creature, Speed speed, int initiative) =>
        new(owner, creature, speed, Initiative.Of(initiative));
}
