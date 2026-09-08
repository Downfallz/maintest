using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Infrastructure.Randomness;

/// <summary>
/// The engine's randomness, seeded so that a match or a simulation replays identically for the same seed.
/// </summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);

    public int Seed { get; } = seed;

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "The exclusive maximum must be greater than the inclusive minimum.");
        }

        return _random.Next(minInclusive, maxExclusive);
    }

    public double NextDouble() => _random.NextDouble();
}
