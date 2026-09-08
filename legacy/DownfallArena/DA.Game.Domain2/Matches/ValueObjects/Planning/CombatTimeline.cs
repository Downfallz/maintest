using DA.Game.Domain2.Matches.ValueObjects.Planning;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

/// <summary>
/// Immutable, ordered activation timeline for a single round.
/// The ordering (Quick/Standard + Initiative) is assumed to be precomputed
/// by a higher-level planner.
/// </summary>
public sealed record CombatTimeline
{
    private readonly IReadOnlyList<ActivationSlot> _slots;

    /// <summary>
    /// Ordered list of activation slots for the round.
    /// </summary>
    public IReadOnlyList<ActivationSlot> Slots => _slots;

    private CombatTimeline(IReadOnlyList<ActivationSlot> slots)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
    }

    /// <summary>
    /// Create an empty timeline.
    /// </summary>
    public static CombatTimeline Empty { get; } =
        new(Array.Empty<ActivationSlot>());

    /// <summary>
    /// Create a timeline from an ordered sequence of slots.
    /// The sequence is assumed to already be sorted by Quick/Standard + Initiative.
    /// </summary>
    public static CombatTimeline FromSlots(IEnumerable<ActivationSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        // Materialize once to avoid external mutation.
        var materialized = slots as ActivationSlot[] ?? slots.ToArray();
        return new CombatTimeline(materialized);
    }

    /// <summary>
    /// Number of slots in this timeline.
    /// </summary>
    public int Count => _slots.Count;

    /// <summary>
    /// Access a slot by index.
    /// </summary>
    public ActivationSlot this[int index] => _slots[index];

    public override string ToString()
        => $"CombatTimeline[{Count} slots]";
}
