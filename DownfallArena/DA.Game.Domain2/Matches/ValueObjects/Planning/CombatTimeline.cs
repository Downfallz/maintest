using DA.Game.Domain2.Matches.ValueObjects.Planning;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record CombatTimeline
{
    /// <summary>
    /// Ordered list of activation slots for the round.
    /// </summary>
    public IReadOnlyList<ActivationSlot> Slots { get; }

    private CombatTimeline(IReadOnlyList<ActivationSlot> slots)
    {
        Slots = slots;
    }

    /// <summary>
    /// Create an empty timeline.
    /// </summary>
    public static CombatTimeline Empty { get; } =
        new(Array.Empty<ActivationSlot>());

    /// <summary>
    /// Create a timeline from a list of slots.
    /// The list is assumed to already be sorted by Quick/Standard + Initiative.
    /// </summary>
    public static CombatTimeline FromSlots(IReadOnlyList<ActivationSlot> slots)
        => new(slots);

    /// <summary>
    /// Number of slots in this timeline.
    /// </summary>
    public int Count => Slots.Count;

    /// <summary>
    /// Access a slot by index.
    /// </summary>
    public ActivationSlot this[int index] => Slots[index];

    public override string ToString()
        => $"CombatTimeline[{Count} slots]";
}
