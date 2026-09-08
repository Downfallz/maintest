using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record CriticalChance(double Value) : ValueObject
{
    public static CriticalChance Of(double v)
    {
        var res = Validate((v is >= 0 and <= 1, "Critical chance must be in [0,1]."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new(v);
    }


    public CriticalChance WithAdded(double delta)
        => Of(Value + delta);

    public CriticalChance WithSubtracted(double delta)
        => Of(Value - delta);

    public double ToPercent() => Value * 100.0;

    public override string ToString() => $"{ToPercent():0.##}%";

}
