namespace DownfallArena.Domain.Resources.Effects;

public sealed record InitiativeDebuff : LastingEffect
{
    private InitiativeDebuff(int amount, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static InitiativeDebuff Of(int amount, Duration duration, StackingPolicy stacking = StackingPolicy.Stack)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new InitiativeDebuff(amount, duration, stacking);
    }
}
