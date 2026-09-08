namespace DownfallArena.SharedKernel.Stats;

public sealed record Energy : NonNegativeStat<Energy>
{
    private Energy(int value)
        : base(value)
    {
    }

    public static Energy Of(int value) => new(value);
}
