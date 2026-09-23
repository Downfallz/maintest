using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Tests.Matches.Support;

/// <summary>
/// A random source that answers integer draws with the given rolls, in order, and fails the test if asked for
/// more than it was given. Doubles are never asked of a timeline, so it refuses them too.
/// </summary>
internal sealed class ScriptedRolls(params int[] rolls) : IRandomSource
{
    private readonly Queue<int> _rolls = new(rolls);

    public int Remaining => _rolls.Count;

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (!_rolls.TryDequeue(out var roll))
        {
            throw new InvalidOperationException("A roll was asked for that the test did not script.");
        }

        if (roll < minInclusive || roll >= maxExclusive)
        {
            throw new InvalidOperationException($"Roll {roll} is outside [{minInclusive}, {maxExclusive}).");
        }

        return roll;
    }

    public double NextDouble() => throw new InvalidOperationException("A timeline never draws a double.");
}
