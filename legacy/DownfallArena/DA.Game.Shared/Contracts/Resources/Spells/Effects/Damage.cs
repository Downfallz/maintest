using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public sealed record Damage : Effect, IInstantEffect
{
    public int Amount { get; }

    private Damage(int amount) : base(EffectKind.Damage) => Amount = amount;

    public static Damage Of(int amount)
    {
        var res = Validate((amount > 0, "Damage amount must be > 0."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new Damage(amount);
    }
}
