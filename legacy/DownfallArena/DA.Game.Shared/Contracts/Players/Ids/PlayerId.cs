namespace DA.Game.Shared.Contracts.Players.Ids;

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());

    public static PlayerId Of(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(value), "PlayerId cannot be Guid.Empty.");

        return new PlayerId(value);
    }

    public override string ToString() => Value.ToString();
}
