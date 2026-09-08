namespace DownfallArena.Domain.Resources.Effects;

public sealed record Damage : InstantEffect
{
    private Damage(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }

    public static Damage Of(int amount) => new(Positive(amount, nameof(amount)));
}
