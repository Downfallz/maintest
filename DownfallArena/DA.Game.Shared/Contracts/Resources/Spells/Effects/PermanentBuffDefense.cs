using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public sealed record PermanentBuffDefense : Effect, IPermanentEffect
{
    public int Amount { get; }

    private PermanentBuffDefense(int amount, TargetingSpec targeting)
        : base(targeting) => Amount = amount;

    public static PermanentBuffDefense Of(int amount, TargetingSpec targeting)
    {
        var res = Validate((amount > 0, "Buff amount must be > 0."),
                        (targeting is not null, "Targeting required."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new PermanentBuffDefense(amount, targeting!);
    }

    public static PermanentBuffDefense Self(int amount)
        => Of(amount, TargetingSpec.Of(TargetOrigin.Self, TargetScope.SingleTarget, 1));
}
