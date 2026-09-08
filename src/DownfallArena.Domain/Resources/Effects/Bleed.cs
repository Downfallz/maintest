namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Damage applied to the target at the start of each of its rounds while the condition lasts.
/// </summary>
public sealed record Bleed : LastingEffect
{
    private Bleed(int amountPerRound, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        AmountPerRound = amountPerRound;
    }

    public int AmountPerRound { get; }

    public static Bleed Of(int amountPerRound, int rounds, StackingPolicy stacking = StackingPolicy.Refresh) =>
        new(Positive(amountPerRound, nameof(amountPerRound)), Duration.OfRounds(rounds), stacking);
}
