using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Ports;

/// <summary>
/// Creates a random source for a seed, so every match and every agent can replay from the seed recorded
/// with its result. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IRandomSourceFactory
{
    IRandomSource Create(int seed);
}
