namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a match.
/// </summary>
public readonly record struct MatchId
{
    private MatchId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static MatchId New() => new(Guid.CreateVersion7());

    public static MatchId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A MatchId cannot be the empty Guid.", nameof(value));
        }

        return new MatchId(value);
    }

    public override string ToString() => Value.ToString();
}
