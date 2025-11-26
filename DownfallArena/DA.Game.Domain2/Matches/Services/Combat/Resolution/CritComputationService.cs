using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat;

/// <summary>
/// Crit policy V1: no crit, just passes through the raw effects.
/// </summary>
public sealed class CritComputationService : ICritComputationService
{
    private readonly IRandom _rng;

    public CritComputationService(IRandom rng)
    {
        _rng = rng;
    }
    public CritComputationResult ApplyCrit(GameContext ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);
        // Compute total crit chance
        var baseCrit = ctx.Actor.BaseCritical.Value;   // int 0–100
        var bonusCrit = ctx.Actor.BonusCritical.Value;  // int 0–100
        var spellBonus = choice.SpellRef.CritChance.Value;          // int 0–100 (adjust name to your model)

        var totalChancePercent = baseCrit + bonusCrit + spellBonus;
        var chance = Math.Clamp(totalChancePercent / 100.0, 0.0, 1.0);

        // Roll RNG
        var roll = _rng.NextDouble(); // 0.0–1.0

        if (roll <= chance)
        {
            // Critical
            var multiplier = spellBonus; // e.g. 2.0, 1.5, etc.
            return CritComputationResult.Critical(
                chanceUsed: chance,
                roll: roll,
                multiplier: multiplier);
        }

        // Normal hit
        return CritComputationResult.Normal(
            chanceUsed: chance,
            roll: roll);
    }
}