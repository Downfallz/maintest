namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Lowers a creature's Defense while it lasts (ADR 0035). <see cref="DefenseBuff"/>'s mirror, and the same
/// shape as <see cref="InitiativeDebuff"/>: an amount, a duration that may be permanent, and a stacking policy.
/// </summary>
public sealed record DefenseDebuff : LastingEffect
{
    private DefenseDebuff(int amount, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static DefenseDebuff Of(int amount, Duration duration, StackingPolicy stacking = StackingPolicy.Stack)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new DefenseDebuff(amount, duration, stacking);
    }
}
