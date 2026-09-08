using DownfallArena.Application.Ports;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Tests.Support;

internal sealed class TestRandomFactory : IRandomSourceFactory
{
    public List<int> Seeds { get; } = [];

    public IRandomSource Create(int seed)
    {
        Seeds.Add(seed);
        return new TestRandom(unchecked((uint)seed));
    }
}
