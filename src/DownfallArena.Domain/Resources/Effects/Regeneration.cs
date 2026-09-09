namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Healing applied to the target at the start of each of its rounds while the condition lasts: the healing
/// counterpart of <see cref="Bleed"/> (ADR 0019).
/// </summary>
public sealed record Regeneration : LastingEffect
{
    private Regeneration(int amountPerRound, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        AmountPerRound = amountPerRound;
    }

    public int AmountPerRound { get; }

    public static Regeneration Of(int amountPerRound, int rounds, StackingPolicy stacking = StackingPolicy.Refresh)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amountPerRound, 1);
        return new Regeneration(amountPerRound, Duration.OfRounds(rounds), stacking);
    }
}
