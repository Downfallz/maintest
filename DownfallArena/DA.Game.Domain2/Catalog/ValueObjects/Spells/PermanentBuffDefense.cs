using DA.Game.Domain2.Shared.Resources.Enums;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells;

public sealed record PermanentBuffDefense : Effect, IInstantEffect
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

        return new PermanentBuffDefense(amount, targeting);
    }

    public static PermanentBuffDefense Self(int amount)
        => Of(amount, TargetingSpec.Of(TargetOrigin.Self, TargetScope.Single, 1));
}
