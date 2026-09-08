namespace DA.Game.Shared.Contracts.Matches.Ids;

public readonly record struct RoundId(int Value)
{
    public static RoundId New(int turnNumber)
    {
        if (turnNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(turnNumber), "Round number must be greater than 0.");

        return new RoundId(turnNumber);
    }

    public override string ToString() => Value.ToString();

    public RoundId Next() => new(Value + 1);

    public static RoundId First => new(1);
}
