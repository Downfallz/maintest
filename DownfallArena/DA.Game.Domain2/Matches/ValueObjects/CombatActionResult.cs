using DA.Game.Domain2.Matches.Services.Combat.Execution;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CombatActionResult(
    CombatActionChoice choice,
    IReadOnlyList<InstantEffectApplication> InstantEffects,
    bool WasCritical);
    //,IReadOnlyList<ConditionApplication> NewConditions);
