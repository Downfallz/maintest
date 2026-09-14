namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// The target skips its activation slots while the condition lasts.
/// </summary>
public sealed record Stun : LastingEffect
{
    private Stun(Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
    }

    public static Stun For(int rounds, StackingPolicy stacking = ForRoundsDefault) =>
        new(Duration.OfRounds(rounds), stacking);
}
