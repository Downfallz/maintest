namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Energy given to the target at the start of each of its rounds while the condition lasts: the energy
/// counterpart of <see cref="Bleed"/> and <see cref="Regeneration"/> (ADR 0020). It stacks on top of the
/// energy every creature already gains in the EnergyGain sub-phase.
/// </summary>
public sealed record EnergyRegeneration : LastingEffect
{
    private EnergyRegeneration(int amountPerRound, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        AmountPerRound = amountPerRound;
    }

    public int AmountPerRound { get; }

    public static EnergyRegeneration Of(int amountPerRound, int rounds, StackingPolicy stacking = StackingPolicy.Refresh)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amountPerRound, 1);
        return new EnergyRegeneration(amountPerRound, Duration.OfRounds(rounds), stacking);
    }
}
