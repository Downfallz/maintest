using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A random source that replays the given doubles in order, then repeats the last one. Integers count down from
/// the top of the range and wrap, so creatures rolling off a tie never roll the same number and the first to
/// roll -- Player 1's, lowest creature id first -- acts first.
/// </summary>
internal sealed class FixedRandom(params double[] doubles) : IRandomSource
{
    private readonly Queue<double> _doubles = new(doubles);
    private double _last = doubles.Length == 0 ? 0.5 : doubles[^1];
    private int _integers;

    public int NextInt32(int minInclusive, int maxExclusive) =>
        maxExclusive - 1 - (_integers++ % (maxExclusive - minInclusive));

    public double NextDouble()
    {
        if (_doubles.TryDequeue(out var next))
        {
            _last = next;
        }

        return _last;
    }
}
