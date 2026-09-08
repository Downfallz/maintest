using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record CombatActionResult : ValueObject
{
    public CombatActionChoice OriginalChoice { get; init; }
    public CombatActionChoice EffectiveChoice { get; init; }
    public IReadOnlyList<InstantEffectApplication> InstantEffects { get; init; }
    public IReadOnlyList<ConditionApplication> OvertimeEffects { get; init; }
    public CritComputationResult Critical { get; init; }
    public IReadOnlyList<TargetingFailure> TargetingFailures { get; init; }

    public bool HasPartialTargetFailures => TargetingFailures.Count > 0;

    public CombatActionResult(
        CombatActionChoice originalChoice,
        CombatActionChoice effectiveChoice,
        IReadOnlyList<InstantEffectApplication>? instantEffects,
        IReadOnlyList<ConditionApplication>? overtimeEffects,
        CritComputationResult critical,
        IReadOnlyList<TargetingFailure>? targetingFailures)
    {
        OriginalChoice = originalChoice ?? throw new ArgumentNullException(nameof(originalChoice));
        EffectiveChoice = effectiveChoice ?? throw new ArgumentNullException(nameof(effectiveChoice));
        InstantEffects = instantEffects ?? Array.Empty<InstantEffectApplication>();
        OvertimeEffects = overtimeEffects ?? Array.Empty<ConditionApplication>();
        Critical = critical ?? CritComputationResult.Normal(0.0, 0.0);
        TargetingFailures = targetingFailures ?? Array.Empty<TargetingFailure>();
    }
}
