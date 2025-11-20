using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Stats;

public sealed record CriticalChance(Percentage01 Value) : ValueObject {
    public static CriticalChance Of(Percentage01 v)
    {
        return new(v);
    }
    public override string ToString() => Value.ToString();
}
