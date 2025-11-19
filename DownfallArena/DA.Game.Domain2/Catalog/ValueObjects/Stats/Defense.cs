using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects.Stats;

public sealed record Defense(int Value) : ValueObject
{
    public static Defense Of(int v)
    {
        var res = Validate((v >= 0, "Defense >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
