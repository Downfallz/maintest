using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects.Stats;

public sealed record CriticalChance(Percentage01 Value) : ValueObject
{
    public static CriticalChance Of(Percentage01 v)
    {
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
