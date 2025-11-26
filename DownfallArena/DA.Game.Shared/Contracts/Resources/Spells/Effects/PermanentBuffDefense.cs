using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public sealed record PermanentBuffDefense : Effect, IPermanentEffect
{
    public int Amount { get; }

    private PermanentBuffDefense(int amount)
        : base(Matches.Enums.EffectKind.Buff) => Amount = amount;

    public static PermanentBuffDefense Of(int amount)
    {
        var res = Validate((amount > 0, "Buff amount must be > 0."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new PermanentBuffDefense(amount);
    }

    public static PermanentBuffDefense Self(int amount)
        => Of(amount);
}
