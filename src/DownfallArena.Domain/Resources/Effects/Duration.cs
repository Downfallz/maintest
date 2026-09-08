using System.Globalization;

namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// How long a lasting effect stays on a creature: a number of rounds, or permanently.
/// </summary>
public readonly record struct Duration
{
    private Duration(int? rounds)
    {
        Rounds = rounds;
    }

    /// <summary>
    /// Number of rounds, or <c>null</c> when permanent.
    /// </summary>
    public int? Rounds { get; }

    public bool IsPermanent => Rounds is null;

    public static Duration Permanent => new(null);

    public static Duration OfRounds(int rounds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rounds, 1);
        return new Duration(rounds);
    }

    public override string ToString() =>
        Rounds is { } rounds ? rounds.ToString(CultureInfo.InvariantCulture) + " rounds" : "permanent";
}
