namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// The target skips its activation slots while the condition lasts. A second stun on a stunned creature is
/// ignored, and so is one in the round of immunity after a stun ends (ADR 0072, which retired the restart of
/// ADR 0041): the policy is <see cref="StackingPolicy.Ignore"/> so the effect says what the creature enforces.
/// </summary>
public sealed record Stun : LastingEffect
{
    private Stun(Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
    }

    /// <summary>A stun of a number of rounds. Its policy is always <see cref="StackingPolicy.Ignore"/>: the creature enforces nothing else (ADR 0072).</summary>
    public static Stun For(int rounds) =>
        new(Duration.OfRounds(rounds), StackingPolicy.Ignore);
}
