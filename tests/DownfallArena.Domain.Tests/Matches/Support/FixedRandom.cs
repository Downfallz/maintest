using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A random source that replays the given doubles in order, then repeats the last one. Integers are the minimum.
/// </summary>
internal sealed class FixedRandom(params double[] doubles) : IRandomSource
{
    private readonly Queue<double> _doubles = new(doubles);
    private double _last = doubles.Length == 0 ? 0.5 : doubles[^1];

    public int NextInt32(int minInclusive, int maxExclusive) => minInclusive;

    public double NextDouble()
    {
        if (_doubles.TryDequeue(out var next))
        {
            _last = next;
        }

        return _last;
    }
}
