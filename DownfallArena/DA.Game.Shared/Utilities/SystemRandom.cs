using System.Security.Cryptography;

namespace DA.Game.Shared.Utilities;

public sealed class SystemRandom : IRandom
{
    public int Next(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max");
        if (min == max)
            return min;

        // Generate a random int in [min, max)
        var diff = (long)max - min;
        var uint32 = RandomNumberGenerator.GetInt32((int)diff);
        return min + uint32;
    }
}
