namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Takes energy from a creature at once (ADR 0035). <see cref="EnergyGain"/>'s mirror: it takes at most what
/// the target has, so a drain of two against one energy takes one.
/// </summary>
public sealed record EnergyDrain : InstantEffect
{
    private EnergyDrain(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static EnergyDrain Of(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new EnergyDrain(amount);
    }
}
