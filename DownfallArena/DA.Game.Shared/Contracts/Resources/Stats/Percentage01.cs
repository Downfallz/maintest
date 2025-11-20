using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Percentage01(double Value) : ValueObject
{
    public static Percentage01 Of(double v)
    {
        var res = Validate((v is >= 0 and <= 1, "Must be in [0,1]"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new(v);
    }
    public double ToPercent() => Value * 100.0;
    public override string ToString() => $"{Value:0.##}";
}
