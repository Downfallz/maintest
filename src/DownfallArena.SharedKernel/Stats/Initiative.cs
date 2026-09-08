namespace DownfallArena.SharedKernel.Stats;

public sealed record Initiative : NonNegativeStat<Initiative>
{
    private Initiative(int value)
        : base(value)
    {
    }

    public static Initiative Of(int value) => new(value);
}
