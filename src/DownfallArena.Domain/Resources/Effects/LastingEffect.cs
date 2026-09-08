namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// An effect that attaches to the target as a condition for a duration.
/// </summary>
public abstract record LastingEffect : Effect
{
    protected LastingEffect(Duration duration, StackingPolicy stacking)
    {
        Duration = duration;
        Stacking = stacking;
    }

    public Duration Duration { get; }

    public StackingPolicy Stacking { get; }
}
