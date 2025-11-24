using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public sealed record Damage : Effect, IInstantEffect
{
    public int Amount { get; }

    private Damage(int amount, TargetingSpec targeting) : base(EffectKind.Damage, targeting) => Amount = amount;

    public static Damage Of(int amount, TargetingSpec targeting)
    {
        var res = Validate((amount > 0, "Damage amount must be > 0."),
                            (targeting is not null, "Targeting required."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new Damage(amount, targeting!);
    }

    public static Damage SingleTargetEnemy(int amount)
        => Of(amount, TargetingSpec.Of(TargetOrigin.Enemy, TargetScope.SingleTarget, 1));

    public Damage WithTargeting(TargetingSpec targeting) => Of(Amount, targeting);
}
