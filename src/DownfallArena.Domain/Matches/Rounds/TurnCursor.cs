using System.Globalization;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// An immutable position in the combat timeline.
/// </summary>
public readonly record struct TurnCursor
{
    private TurnCursor(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public static TurnCursor Start => new(0);

    public TurnCursor MoveNext() => new(Index + 1);

    public bool IsEnd(int totalSlots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalSlots);
        return Index >= totalSlots;
    }

    public override string ToString() => Index.ToString(CultureInfo.InvariantCulture);
}
