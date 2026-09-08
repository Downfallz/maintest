using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A small deterministic generator (a linear congruential sequence) so tests replay without Infrastructure.
/// </summary>
internal sealed class TestRandom(uint seed) : IRandomSource
{
    private uint _state = seed;

    public int NextInt32(int minInclusive, int maxExclusive) =>
        minInclusive + (int)((Next() >> 16) % (uint)(maxExclusive - minInclusive));

    public double NextDouble() => Next() / 4294967296.0;

    private uint Next()
    {
        _state = unchecked((_state * 1664525u) + 1013904223u);
        return _state;
    }
}
