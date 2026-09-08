namespace DownfallArena.Domain.Resources.Effects;

public sealed record EnergyGain : InstantEffect
{
    private EnergyGain(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static EnergyGain Of(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new EnergyGain(amount);
    }
}
