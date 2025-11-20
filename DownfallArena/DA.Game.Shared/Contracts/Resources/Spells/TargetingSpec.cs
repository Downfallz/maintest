using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells;

/// <summary>
/// Specifies how an effect selects its targets (who, and how many).
/// </summary>
public sealed record TargetingSpec : ValueObject
{
    public TargetOrigin Origin { get; }
    public TargetScope Scope { get; }
    public int? MaxTargets { get; }

    private TargetingSpec(TargetOrigin origin, TargetScope scope, int? maxTargets)
    {
        Origin = origin;
        Scope = scope;
        MaxTargets = maxTargets;
    }

    public static TargetingSpec Of(TargetOrigin origin, TargetScope scope, int? maxTargets = 1)
    {
        var res = Validate((maxTargets is null or > 0, "MaxTargets must be > 0 when provided."));
        if (!res.IsSuccess)
            throw new ArgumentException(res.Error);

        return new TargetingSpec(origin, scope, maxTargets);
    }
}
