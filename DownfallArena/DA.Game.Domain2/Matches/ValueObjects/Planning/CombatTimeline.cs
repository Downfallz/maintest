using DA.Game.Domain2.Matches.ValueObjects.Planning;

public sealed record CombatTimeline
{
    public IReadOnlyList<ActivationSlot> Slots { get; init; }
    public TurnCursor Cursor { get; init; }

    private CombatTimeline(
        IReadOnlyList<ActivationSlot> slots,
        TurnCursor cursor)
    {
        Slots = slots;
        Cursor = cursor;
    }

    public static CombatTimeline Empty =>
        new(Array.Empty<ActivationSlot>(), TurnCursor.Start);

    public static CombatTimeline FromSlots(IReadOnlyList<ActivationSlot> slots) =>
        new(slots, TurnCursor.Start);

    public ActivationSlot? Current =>
        Cursor.IsEnd(Slots.Count) ? null : Slots[Cursor.Index];

    public bool IsComplete => Cursor.IsEnd(Slots.Count);

    public CombatTimeline MoveNext()
    {
        if (IsComplete)
            return this;

        return this with { Cursor = Cursor.MoveNext() };
    }

    public CombatTimeline ResetCursor()
        => this with { Cursor = Cursor.Reset() };
}
