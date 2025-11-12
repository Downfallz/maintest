using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells.Effects.Base;

/// <summary>Spécifie comment un effet choisit ses cibles (qui, et combien).</summary>
public sealed record TargetingSpec(TargetOrigin Origin, TargetScope Scope, int? MaxTargets = 1) : ValueObject
{
    public static TargetingSpec Of(TargetOrigin origin, TargetScope scope, int? maxTargets = 1)
    {
        var res = Validate((maxTargets is null or > 0, "MaxTargets must be > 0 when provided."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new TargetingSpec(origin, scope, maxTargets);
    }
}
