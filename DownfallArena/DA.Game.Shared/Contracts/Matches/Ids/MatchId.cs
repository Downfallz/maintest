namespace DA.Game.Domain2.Matches.Ids;

public readonly record struct MatchId(Guid Value)
{
    public static MatchId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
