using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Every targeting failure of an action at once, so a player can fix them all and the resolution can drop
/// individual targets while keeping the action.
/// </summary>
public sealed record TargetingReport(IReadOnlyList<TargetingFailure> Failures)
{
    public static TargetingReport Clean { get; } = new([]);

    public bool IsClean => Failures.Count == 0;

    public IEnumerable<TargetingFailure> GlobalFailures => Failures.Where(failure => failure.IsGlobal);

    public IEnumerable<TargetingFailure> PerTargetFailures => Failures.Where(failure => !failure.IsGlobal);

    public IReadOnlySet<CreatureId> InvalidTargets => PerTargetFailures.Select(failure => failure.Target).OfType<CreatureId>().ToHashSet();

    public TargetingFailure? FirstFailure => GlobalFailures.FirstOrDefault() ?? PerTargetFailures.FirstOrDefault();
}
