using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using System;

namespace DA.Game.Domain2.Matches.Services.Combat;

/// <summary>
/// Service V1 that computes whether a combat action is a critical hit.
/// </summary>
public sealed class CritComputationServiceV1 : ICritComputationService
{
    private const double CritMultiplier = 2.0;

    private readonly IRandom _rng;

    public CritComputationServiceV1(IRandom rng)
    {
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public CritComputationResult ApplyCrit(CreaturePerspective ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        // 1) Aggregate crit chances as probabilities [0.0, 1.0]
        var baseCrit = ctx.Actor.BaseCritical.Value;      // 0.0–1.0
        var bonusCrit = ctx.Actor.BonusCritical.Value;     // 0.0–1.0
        var spellCrit = choice.SpellRef.CritChance.Value;  // 0.0–1.0

        var totalChance = baseCrit + bonusCrit + spellCrit;

        // 2) Clamp to [0.0, 1.0]
        var critProbability = Math.Clamp(totalChance, 0.0, 1.0);

        // 3) Roll RNG in [0.0, 1.0)
        var roll = _rng.NextDouble();

        if (roll < critProbability)
        {
            // Critical hit
            return CritComputationResult.Critical(
                chanceUsed: critProbability,
                roll: roll,
                multiplier: CritMultiplier);
        }

        // Normal hit
        return CritComputationResult.Normal(
            chanceUsed: critProbability,
            roll: roll);
    }
}
