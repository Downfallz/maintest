using DownfallArena.Application.Ports;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Infrastructure.Randomness;

public sealed class SeededRandomSourceFactory : IRandomSourceFactory
{
    public IRandomSource Create(int seed) => new SeededRandomSource(seed);
}
