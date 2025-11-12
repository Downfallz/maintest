using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects;

public sealed record NonNegativeInt(int Value) : ValueObject
{
    public static NonNegativeInt Of(int v) { 
        var res = Validate((v >= 0, "Must be >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);
        return new(v); 
    }

    public override string ToString() => Value.ToString();
}
