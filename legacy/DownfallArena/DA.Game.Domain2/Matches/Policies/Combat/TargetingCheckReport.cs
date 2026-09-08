using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed record TargetingCheckReport(
    IReadOnlyList<TargetingFailure> Failures)
{
    public bool HasErrors => Failures.Count > 0;
    public bool HasSingleError => Failures.Count == 1;

    public IReadOnlyList<TargetingFailure> GlobalFailures =>
        Failures.Where(f => f.TargetId is null).ToList();

    public IReadOnlyList<TargetingFailure> PerTargetFailures =>
        Failures.Where(f => f.TargetId is not null).ToList();

    public IReadOnlyList<CreatureId?> InvalidTargetIds =>
        PerTargetFailures
            .Where(f => f.TargetId is not null)
            .Select(f => f.TargetId!)
            .Distinct()
            .ToList();
}