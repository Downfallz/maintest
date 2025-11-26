namespace DA.Game.Domain2.Matches.ValueObjects.Planning;

public sealed record TurnCursor(int Index)
{
    public static TurnCursor Start => new(0);

    public bool IsEnd => Index < 0;

    public TurnCursor MoveNext(CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var next = timeline.NextAfter(Index);
        return next is null ? new TurnCursor(-1) : new TurnCursor(Index + 1);
    }
}

