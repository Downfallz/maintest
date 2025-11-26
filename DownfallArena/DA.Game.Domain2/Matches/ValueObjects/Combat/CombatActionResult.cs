using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Combat;

public sealed record CombatActionResult : ValueObject
{
    public CombatActionChoice Choice { get; init; }
    public IReadOnlyList<InstantEffectApplication> InstantEffects { get; init; }
    public CritComputationResult Critical { get; init; }   // ⬅️ Nouvelle propriété

    public CombatActionResult(
        CombatActionChoice choice,
        IReadOnlyList<InstantEffectApplication>? instantEffects,
        CritComputationResult critical)
    {
        Choice = choice;
        InstantEffects = instantEffects ?? Array.Empty<InstantEffectApplication>();
        Critical = critical ?? CritComputationResult.Normal(0.0, 0.0);
    }
}

