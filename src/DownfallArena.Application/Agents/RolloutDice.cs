using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The dice of one rollout: SplitMix64 from a seed, so every candidate a decision compares can be played on
/// the same rolls (ADR 0094). Two purchases read on different dice differ by the dice as much as by the
/// purchase; read on the same ones, only the purchase is left between them.
/// </summary>
internal sealed class RolloutDice(ulong seed) : IRandomSource
{
    private ulong _state = seed;

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "The exclusive maximum must be greater than the inclusive minimum.");
        }

        return minInclusive + (int)(Next() % (ulong)((long)maxExclusive - minInclusive));
    }

    public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

    private ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var mixed = _state;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
        return mixed ^ (mixed >> 31);
    }
}
