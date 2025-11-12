using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects;

public sealed record Level(int Value) : ValueObject
{
    public static Level Of(int v) { 
        var res = Validate((v >= 0, "Level >= 0"));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new(v); 
    }
    public override string ToString() => Value.ToString();
}
