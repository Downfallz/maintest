using DownfallArena.Application.Learning;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Learning;

public sealed class BoardSlotsTests
{
    [Fact]
    public void Own_creatures_take_the_first_slots_and_enemies_the_next()
    {
        var board = Boards.Board(
            PlayerSlot.Player2,
            [Boards.Creature(3, PlayerSlot.Player2), Boards.Creature(4, PlayerSlot.Player2)],
            [Boards.Creature(1, PlayerSlot.Player1), Boards.Creature(2, PlayerSlot.Player1)]);

        var slots = BoardSlots.Of(board, teamSize: 2);

        slots.TeamSize.ShouldBe(2);
        slots.SlotOf(CreatureId.From(3)).ShouldBe(0);
        slots.SlotOf(CreatureId.From(4)).ShouldBe(1);
        slots.SlotOf(CreatureId.From(1)).ShouldBe(2);
        slots.SlotOf(CreatureId.From(2)).ShouldBe(3);
        slots.IsOwn(1).ShouldBeTrue();
        slots.IsOwn(2).ShouldBeFalse();
    }

    [Fact]
    public void A_team_smaller_than_the_team_size_leaves_its_last_slots_empty()
    {
        var board = Boards.Board(PlayerSlot.Player1, [Boards.Creature(1, PlayerSlot.Player1)], [Boards.Creature(3, PlayerSlot.Player2)]);

        var slots = BoardSlots.Of(board, teamSize: 3);

        slots.SlotOf(CreatureId.From(1)).ShouldBe(0);
        slots.SlotOf(CreatureId.From(3)).ShouldBe(3);
    }

    [Fact]
    public void The_mask_has_one_bit_per_slot()
    {
        var board = Boards.Board(
            PlayerSlot.Player1,
            [Boards.Creature(1, PlayerSlot.Player1), Boards.Creature(2, PlayerSlot.Player1)],
            [Boards.Creature(3, PlayerSlot.Player2), Boards.Creature(4, PlayerSlot.Player2)]);
        var slots = BoardSlots.Of(board, teamSize: 2);

        slots.MaskOf([]).ShouldBe(0);
        slots.MaskOf([CreatureId.From(1)]).ShouldBe(0b0001);
        slots.MaskOf([CreatureId.From(4), CreatureId.From(3)]).ShouldBe(0b1100);
    }

    [Fact]
    public void Creatures_off_the_board_and_oversized_teams_are_refused()
    {
        var board = Boards.Board(PlayerSlot.Player1, [Boards.Creature(1, PlayerSlot.Player1)], [Boards.Creature(3, PlayerSlot.Player2)]);
        var slots = BoardSlots.Of(board, teamSize: 1);

        Should.Throw<ArgumentOutOfRangeException>(() => slots.SlotOf(CreatureId.From(9)));
        Should.Throw<ArgumentOutOfRangeException>(() => slots.MaskOf([CreatureId.From(9)]));
        Should.Throw<ArgumentNullException>(() => slots.MaskOf(null!));
        Should.Throw<ArgumentNullException>(() => BoardSlots.Of(null!, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => BoardSlots.Of(board, 0));
        Should.Throw<InvalidOperationException>(() => BoardSlots.Of(board with { Allies = [Boards.Creature(1, PlayerSlot.Player1), Boards.Creature(2, PlayerSlot.Player1)] }, 1));
    }
}
