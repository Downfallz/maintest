namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Raises every direct hit its holder deals by an amount, for a duration or for good (ADR 0101): added to a
/// <see cref="Damage"/> effect's amount before the critical multiplier and before the target's defense. A
/// bleed is not a hit, and what a cast does to its own caster is not one either.
/// </summary>
public sealed record DamageBuff : LastingEffect
{
    private DamageBuff(int amount, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static DamageBuff Of(int amount, Duration duration, StackingPolicy stacking = StackingPolicy.Stack)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new DamageBuff(amount, duration, stacking);
    }
}
