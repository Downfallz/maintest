using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// Answers every integer draw by counting down from the top of the range, and every double from the source it
/// wraps. A match draws integers only to roll off an initiative tie (ADR 0063), so the first creature to roll --
/// Player 1's, lowest id first -- acts first, and the critical rolls are the ones the wrapped source always gave.
/// For the tests that are about something other than the dice.
/// </summary>
internal sealed class FirstToRollWinsTies(IRandomSource doubles) : IRandomSource
{
    private int _integers;

    public int NextInt32(int minInclusive, int maxExclusive) =>
        maxExclusive - 1 - (_integers++ % (maxExclusive - minInclusive));

    public double NextDouble() => doubles.NextDouble();
}
