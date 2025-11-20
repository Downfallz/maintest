using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Initiative(int Value) : ValueObject
{
    public static Initiative Of(int v)
    {
        var res = Validate((v >= 0, "Initiative >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
