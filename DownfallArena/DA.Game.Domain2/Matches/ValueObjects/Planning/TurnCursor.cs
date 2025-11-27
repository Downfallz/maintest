namespace DA.Game.Domain2.Matches.ValueObjects.Planning;

public sealed record TurnCursor(int Index)
{
    public static TurnCursor Start => new(0);

    // Interpret "end" as: cursor is outside of slots range
    public bool IsEnd(int totalSlots) => Index >= totalSlots || Index < 0;

    public TurnCursor MoveNext() => new(Index + 1);

    public TurnCursor Reset() => new(0);

    public override string ToString() => $"Cursor at {Index}";
}
