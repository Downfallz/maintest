namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a player.
/// </summary>
public readonly record struct PlayerId
{
    private PlayerId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static PlayerId New() => new(Guid.CreateVersion7());

    public static PlayerId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A PlayerId cannot be the empty Guid.", nameof(value));
        }

        return new PlayerId(value);
    }

    public override string ToString() => Value.ToString();
}
