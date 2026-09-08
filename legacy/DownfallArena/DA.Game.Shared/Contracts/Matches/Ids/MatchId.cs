namespace DA.Game.Shared.Contracts.Matches.Ids;

public readonly record struct MatchId(Guid Value)
{
    public static MatchId New() => new(Guid.NewGuid());

    public static MatchId Of(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(value), "MatchId cannot be Guid.Empty.");

        return new MatchId(value);
    }

    public override string ToString() => Value.ToString();
}
