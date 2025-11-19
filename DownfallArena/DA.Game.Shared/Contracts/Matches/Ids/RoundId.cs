namespace DA.Game.Domain2.Matches.Ids;

public readonly record struct RoundId(int Value)
{
    public static RoundId New(int turnNumber) => new(turnNumber);
    public override string ToString() => Value.ToString();
}
