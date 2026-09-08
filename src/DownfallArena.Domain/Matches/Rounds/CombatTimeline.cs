using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// The ordered activation slots of a round. Built by the planning rules from the speed choices.
/// </summary>
public sealed class CombatTimeline
{
    private readonly List<ActivationSlot> _slots;

    private CombatTimeline(List<ActivationSlot> slots)
    {
        _slots = slots;
    }

    public static CombatTimeline Empty { get; } = new([]);

    public IReadOnlyList<ActivationSlot> Slots => _slots;

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

        return new CombatTimeline(list);
    }

    public ActivationSlot? SlotOf(CreatureId creature) => _slots.Find(slot => slot.Creature == creature);
}
