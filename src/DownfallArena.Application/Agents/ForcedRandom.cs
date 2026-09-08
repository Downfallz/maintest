using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// A random source that always rolls the same value, so a resolution can be computed for a forced critical
/// (zero rolls under any positive chance) and for a forced miss (one never does).
/// </summary>
internal sealed class ForcedRandom(double roll) : IRandomSource
{
    public static ForcedRandom Critical { get; } = new(0.0);

    public static ForcedRandom NotCritical { get; } = new(1.0);

    public int NextInt32(int minInclusive, int maxExclusive) => minInclusive;

    public double NextDouble() => roll;
}
