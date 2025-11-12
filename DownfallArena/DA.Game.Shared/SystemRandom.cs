namespace DA.Game.Shared;

public sealed class SystemRandom : IRandom
{
    private readonly Random _r = new();
    public int Next(int min, int max) => _r.Next(min, max);
}
