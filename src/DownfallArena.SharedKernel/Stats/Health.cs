namespace DownfallArena.SharedKernel.Stats;

public sealed record Health : NonNegativeStat<Health>
{
    private Health(int value)
        : base(value)
    {
    }

    public static Health Of(int value) => new(value);
}
