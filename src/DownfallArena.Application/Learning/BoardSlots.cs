using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning;

/// <summary>
/// Where each creature of a board sits in the observation: own creatures at slots 0 to team size minus one,
/// enemies after them. Slots are stable for a match, since teams never change.
/// </summary>
public sealed class BoardSlots
{
    /// <summary>The largest team size a board supports, so that a target mask holds one bit per slot in an <c>int</c>.</summary>
    public const int MaxTeamSize = 16;

    private readonly Dictionary<CreatureId, int> _slots;

    private BoardSlots(Dictionary<CreatureId, int> slots, int teamSize)
    {
        _slots = slots;
        TeamSize = teamSize;
    }

    public int TeamSize { get; }

    public static BoardSlots Of(PlayerBoardState board, int teamSize)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfLessThan(teamSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(teamSize, MaxTeamSize);

        if (board.Allies.Count > teamSize || board.Enemies.Count > teamSize)
        {
            throw new InvalidOperationException($"The board has more creatures than the team size {teamSize} allows.");
        }

        var slots = new Dictionary<CreatureId, int>();
        for (var index = 0; index < board.Allies.Count; index++)
        {
            slots[board.Allies[index].Id] = index;
        }

        for (var index = 0; index < board.Enemies.Count; index++)
        {
            slots[board.Enemies[index].Id] = teamSize + index;
        }

        return new BoardSlots(slots, teamSize);
    }

    public int SlotOf(CreatureId creature) =>
        _slots.TryGetValue(creature, out var slot)
            ? slot
            : throw new ArgumentOutOfRangeException(nameof(creature), creature, "The creature is not on the board.");

    public bool IsOwn(int slot) => slot < TeamSize;

    /// <summary>A bitmask with one bit per board slot.</summary>
    public int MaskOf(IEnumerable<CreatureId> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        return creatures.Aggregate(0, (mask, creature) => mask | (1 << SlotOf(creature)));
    }
}
