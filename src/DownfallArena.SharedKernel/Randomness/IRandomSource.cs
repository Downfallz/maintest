namespace DownfallArena.SharedKernel.Randomness;

/// <summary>
/// The only source of randomness the engine may use. Implementations are seeded so that simulations replay.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Returns an integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
    /// </summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>
    /// Returns a double in [0, 1).
    /// </summary>
    double NextDouble();
}
