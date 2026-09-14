namespace DownfallArena.Domain.Resources.Effects;

/// <summary>
/// Raises a creature's current initiative while it lasts, the mirror of <see cref="InitiativeDebuff"/>
/// (ADR 0036). Buffs and debuffs meet in the same total, which floors at zero.
/// </summary>
public sealed record InitiativeBuff : LastingEffect
{
    private InitiativeBuff(int amount, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static InitiativeBuff Of(int amount, Duration duration, StackingPolicy stacking = WhileLastingDefault)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new InitiativeBuff(amount, duration, stacking);
    }
}
