using System.Globalization;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a round inside a match: its one-based number.
/// </summary>
public readonly record struct RoundId
{
    private RoundId(int number)
    {
        Number = number;
    }

    public int Number { get; }

    public static RoundId First => new(1);

    public static RoundId From(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return new RoundId(number);
    }

    public RoundId Next() => new(Number + 1);

    public override string ToString() => Number.ToString(CultureInfo.InvariantCulture);
}
