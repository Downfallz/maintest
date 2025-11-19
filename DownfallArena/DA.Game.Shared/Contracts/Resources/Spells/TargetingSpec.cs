using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells;

/// <summary>Spécifie comment un effet choisit ses cibles (qui, et combien).</summary>
public sealed record TargetingSpec(TargetOrigin Origin, TargetScope Scope, int? MaxTargets = 1) : ValueObject, ITargetingSpec
{
    public static TargetingSpec Of(TargetOrigin origin, TargetScope scope, int? maxTargets = 1)
    {
        var res = Validate((maxTargets is null or > 0, "MaxTargets must be > 0 when provided."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new TargetingSpec(origin, scope, maxTargets);
    }
}
