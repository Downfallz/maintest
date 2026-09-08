namespace DownfallArena.Domain.Resources.Effects;

public sealed record DefenseBuff : LastingEffect
{
    private DefenseBuff(int amount, Duration duration, StackingPolicy stacking)
        : base(duration, stacking)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static DefenseBuff Of(int amount, Duration duration, StackingPolicy stacking = StackingPolicy.Stack) =>
        new(Positive(amount, nameof(amount)), duration, stacking);
}
