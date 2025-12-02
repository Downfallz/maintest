namespace DA.Game.Domain2.Matches.ValueObjects.Planning;

/// <summary>
/// Immutable cursor used to iterate through a timeline.
/// </summary>
public sealed record TurnCursor
{
    public int Index { get; }

    private TurnCursor(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Cursor index cannot be negative.");

        Index = index;
    }

    /// <summary>
    /// Initial cursor starting at the first slot (index 0).
    /// </summary>
    public static TurnCursor Start => new(0);

    /// <summary>
    /// Create a cursor pointing past the end of available slots.
    /// </summary>
    public static TurnCursor End(int totalSlots)
    {
        if (totalSlots < 0)
            throw new ArgumentOutOfRangeException(nameof(totalSlots));

        return new(totalSlots);
    }

    /// <summary>
    /// Determines whether this cursor is outside the valid slot range.
    /// </summary>
    public bool IsEnd(int totalSlots)
    {
        if (totalSlots < 0)
            throw new ArgumentOutOfRangeException(nameof(totalSlots));

        return Index >= totalSlots;
    }

    /// <summary>
    /// Returns a cursor incremented by 1.
    /// </summary>
    public TurnCursor MoveNext() => new(Index + 1);

    /// <summary>
    /// Resets the cursor to the beginning.
    /// </summary>
    public TurnCursor Reset() => Start;

    /// <summary>
    /// Attempts to advance the cursor only if not already at the end.
    /// Returns the new cursor.
    /// </summary>
    public TurnCursor AdvanceIfPossible(int totalSlots)
    {
        if (IsEnd(totalSlots))
            return this;

        return MoveNext();
    }

    public override string ToString() => $"Cursor[Index={Index}]";
}
