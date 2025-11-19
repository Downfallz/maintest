using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects.Stats;

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
