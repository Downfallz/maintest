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

    /// <summary>
    /// What an effect that applies every round -- <see cref="Bleed" />, <see cref="Regeneration" />,
    /// <see cref="EnergyRegeneration" /> -- does when the creature already carries one of its kind and the
    /// content said nothing: it runs beside it, with its own amount, duration and source (ADR 0040).
    /// </summary>
    public const StackingPolicy PerRoundDefault = StackingPolicy.Stack;

    /// <summary>
    /// What a <see cref="Stun" /> does in the same case: it restarts the one that is there. Its only payload
    /// is a duration, so there is no amount to lose, and two casts must not take two rounds off one creature.
    /// </summary>
    public const StackingPolicy ForRoundsDefault = StackingPolicy.Refresh;

    /// <summary>
    /// What a buff or a debuff does in the same case: another one, added to the same total.
    /// </summary>
    public const StackingPolicy WhileLastingDefault = StackingPolicy.Stack;

    public Duration Duration { get; }

    public StackingPolicy Stacking { get; }
}
