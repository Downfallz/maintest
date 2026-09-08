namespace DownfallArena.SharedKernel.Stats;

public sealed record Defense : NonNegativeStat<Defense>
{
    private Defense(int value)
        : base(value)
    {
    }

    public static Defense Of(int value) => new(value);
}
