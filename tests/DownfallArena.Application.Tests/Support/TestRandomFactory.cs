using System.Collections.Concurrent;
using DownfallArena.Application.Ports;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Tests.Support;

internal sealed class TestRandomFactory : IRandomSourceFactory
{
    private readonly ConcurrentQueue<int> _seeds = new();

    /// <summary>
    /// Every seed this factory was asked for. A batch plays its matches at the same time, so the order they
    /// arrive in is the order the matches happened to reach this and not the order of the seeds: assert on
    /// what is here, never on where.
    /// </summary>
    public IReadOnlyCollection<int> Seeds => _seeds;

    public IRandomSource Create(int seed)
    {
        _seeds.Enqueue(seed);
        return new TestRandom(unchecked((uint)seed));
    }
}
