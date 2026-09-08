using System.Globalization;

namespace DownfallArena.SharedKernel.Stats;

/// <summary>
/// Probability of a critical hit, in [0, 1]. Arithmetic clamps to that range.
/// </summary>
public sealed record CriticalChance
{
    public static readonly CriticalChance None = new(0);

    private CriticalChance(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public double AsPercent => Value * 100;

    public static CriticalChance Of(double value)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A critical chance must be between 0 and 1.");
        }

        return new CriticalChance(value);
    }

    public CriticalChance Plus(double delta) => new(Math.Clamp(Value + delta, 0, 1));

    public CriticalChance Minus(double delta) => new(Math.Clamp(Value - delta, 0, 1));

    public override string ToString() => AsPercent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
