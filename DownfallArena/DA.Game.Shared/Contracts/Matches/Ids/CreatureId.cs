namespace DA.Game.Shared.Contracts.Matches.Ids;

public readonly record struct CreatureId(int Value)
{
    public static CreatureId New(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "CreatureId must be greater than 0.");

        return new CreatureId(value);
    }

    public override string ToString() => Value.ToString();
}
