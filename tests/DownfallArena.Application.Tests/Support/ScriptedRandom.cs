using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// Replays the given values in a loop: each integer draw takes the next value modulo the requested range.
/// </summary>
internal sealed class ScriptedRandom(params uint[] values) : IRandomSource
{
    private int _next;

    public int NextInt32(int minInclusive, int maxExclusive) =>
        minInclusive + (int)(Draw() % (uint)(maxExclusive - minInclusive));

    public double NextDouble() => (Draw() % 1000u) / 1000.0;

    private uint Draw()
    {
        var value = values[_next % values.Length];
        _next++;
        return value;
    }
}
