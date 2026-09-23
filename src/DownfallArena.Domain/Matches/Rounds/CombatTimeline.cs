using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// The ordered activation slots of a round. Built by the planning rules from the speed choices.
/// </summary>
public sealed class CombatTimeline
{
    private readonly List<ActivationSlot> _slots;

    private CombatTimeline(List<ActivationSlot> slots, IReadOnlyList<RollOff> rollOffs)
    {
        _slots = slots;
        RollOffs = rollOffs;
    }

    public static CombatTimeline Empty { get; } = new([], []);

    public IReadOnlyList<ActivationSlot> Slots => _slots;

    /// <summary>
    /// The d20 rolls behind the order, one entry per creature that rolled (ADR 0063). Public like the order
    /// itself: a table sees the dice, and a tie order is given having seen them.
    /// </summary>
    public IReadOnlyList<RollOff> RollOffs { get; }

    public int Count => _slots.Count;

    public ActivationSlot this[int index] => _slots[index];

    public static CombatTimeline Of(IEnumerable<ActivationSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var list = slots.ToList();
        if (list.Select(slot => slot.Creature).Distinct().Count() != list.Count)
        {
            throw new ArgumentException("A creature cannot appear twice in the combat timeline.", nameof(slots));
        }

        return new CombatTimeline(list, []);
    }

    /// <summary>The same order, carrying the rolls that decided it. Every roll must be of a creature on it.</summary>
    public CombatTimeline WithRollOffs(IReadOnlyList<RollOff> rollOffs)
    {
        ArgumentNullException.ThrowIfNull(rollOffs);

        if (rollOffs.Any(rollOff => SlotOf(rollOff.Creature) is null))
        {
            throw new ArgumentException("A roll-off names a creature that is not on the timeline.", nameof(rollOffs));
        }

        return new CombatTimeline(_slots, [.. rollOffs]);
    }

    public ActivationSlot? SlotOf(CreatureId creature) => _slots.Find(slot => slot.Creature == creature);
}
