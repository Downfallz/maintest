using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Energy(int Value) : ValueObject
{
    public static Energy Of(int v)
    {
        var res = Validate((v >= 0, "Energy >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
