namespace DownfallArena.Domain.Resources.Effects;

public sealed record Heal : InstantEffect
{
    private Heal(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static Heal Of(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        return new Heal(amount);
    }
}
