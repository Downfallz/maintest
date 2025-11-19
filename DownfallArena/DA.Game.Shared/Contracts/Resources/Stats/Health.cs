using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record Health(int Value) : ValueObject
{
    public static Health Of(int v)
    {
        var res = Validate((v >= 0, "Health >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
