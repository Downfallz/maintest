using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// What resolving an action computed: either a fizzle with its reason, or the effective targets, the crit roll,
/// the energy to spend, and the outcomes to apply.
/// </summary>
public sealed record CombatResolution
{
    private CombatResolution(
        CombatAction action,
        DomainError? fizzleReason,
        IReadOnlyList<CreatureId> effectiveTargets,
        IReadOnlyList<TargetingFailure> droppedTargets,
        bool isCritical,
        Energy energySpent,
        IReadOnlyList<EffectOutcome> outcomes)
    {
        Action = action;
        FizzleReason = fizzleReason;
        EffectiveTargets = effectiveTargets;
        DroppedTargets = droppedTargets;
        IsCritical = isCritical;
        EnergySpent = energySpent;
        Outcomes = outcomes;
    }

    public CombatAction Action { get; }

    public DomainError? FizzleReason { get; }

    public bool Fizzled => FizzleReason is not null;

    public IReadOnlyList<CreatureId> EffectiveTargets { get; }

    /// <summary>Targets removed at resolution time because they became invalid (dead, for instance).</summary>
    public IReadOnlyList<TargetingFailure> DroppedTargets { get; }

    public bool IsCritical { get; }

    public Energy EnergySpent { get; }

    public IReadOnlyList<EffectOutcome> Outcomes { get; }

    public static CombatResolution Fizzle(CombatAction action, DomainError reason)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(reason);
        return new CombatResolution(action, reason, [], [], false, Energy.Of(0), []);
    }

    public static CombatResolution Resolved(
        CombatAction action,
        IReadOnlyList<CreatureId> effectiveTargets,
        IReadOnlyList<TargetingFailure> droppedTargets,
        bool isCritical,
        Energy energySpent,
        IReadOnlyList<EffectOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(effectiveTargets);
        ArgumentNullException.ThrowIfNull(droppedTargets);
        ArgumentNullException.ThrowIfNull(energySpent);
        ArgumentNullException.ThrowIfNull(outcomes);
        return new CombatResolution(action, null, effectiveTargets, droppedTargets, isCritical, energySpent, outcomes);
    }
}
