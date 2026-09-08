namespace DownfallArena.Domain.Resources.Effects;

public sealed record Damage : InstantEffect
{
    private Damage(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static Damage Of(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new Damage(amount);
    }
}
