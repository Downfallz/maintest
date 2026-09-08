namespace DownfallArena.Domain.Resources;

/// <summary>
/// Who a spell may target and how many targets it takes.
/// </summary>
public sealed record TargetingSpec
{
    private TargetingSpec(TargetOrigin origin, TargetScope scope, int? maxTargets)
    {
        Origin = origin;
        Scope = scope;
        MaxTargets = maxTargets;
    }

    public TargetOrigin Origin { get; }

    public TargetScope Scope { get; }

    /// <summary>
    /// Upper bound on the number of targets; <c>null</c> means every legal target.
    /// </summary>
    public int? MaxTargets { get; }

    public static TargetingSpec SingleTarget(TargetOrigin origin) => new(origin, TargetScope.SingleTarget, 1);

    public static TargetingSpec Multi(TargetOrigin origin, int? maxTargets = null)
    {
        if (maxTargets is < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTargets), maxTargets, "A multi-target spell needs at least two targets, or no bound.");
        }

        return new TargetingSpec(origin, TargetScope.Multi, maxTargets);
    }
}
