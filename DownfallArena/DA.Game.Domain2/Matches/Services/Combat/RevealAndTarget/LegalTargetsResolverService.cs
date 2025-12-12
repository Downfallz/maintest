using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.RevealAndTarget;

public sealed class LegalTargetsResolverService : ILegalTargetsResolverService
{
    public Result<LegalTargetsResult> Resolve(
        CreaturePerspective ctx,
        Spell spellRef)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(spellRef);

        var spec = spellRef.TargetingSpec;

        // Alive only
        var alive = ctx.Creatures.Where(c => c.IsAlive).ToArray();

        // Pool by origin
        IReadOnlyList<CreatureId> pool = spec.Origin switch
        {
            TargetOrigin.Self => new[] { ctx.Actor.CharacterId },

            TargetOrigin.Ally => alive
                .Where(t => t.OwnerSlot == ctx.Actor.OwnerSlot)
                .Select(t => t.CharacterId)
                .ToArray(),

            TargetOrigin.Enemy => alive
                .Where(t => t.OwnerSlot != ctx.Actor.OwnerSlot)
                .Select(t => t.CharacterId)
                .ToArray(),

            TargetOrigin.Any or _ => alive
                .Select(t => t.CharacterId)
                .ToArray()
        };

        // Min/Max targets for UI/AI
        var minTargets = spec.Scope switch
        {
            TargetScope.SingleTarget => 1,
            TargetScope.Multi => 1, // your current policy: at least one
            _ => 1
        };

        var maxTargets = spec.Scope switch
        {
            TargetScope.SingleTarget => 1,
            TargetScope.Multi => spec.MaxTargets ?? pool.Count, // Multi uses MaxTargets
            _ => 1
        };

        // Clamp just in case
        if (maxTargets < minTargets)
            maxTargets = minTargets;

        if (pool.Count < minTargets)
        {
            // No legal way to satisfy the spell now (e.g. no enemies alive)
            return Result<LegalTargetsResult>.Ok(new LegalTargetsResult(
                MinTargets: minTargets,
                MaxTargets: maxTargets,
                LegalTargetIds: Array.Empty<CreatureId>()));
        }

        return Result<LegalTargetsResult>.Ok(new LegalTargetsResult(
            MinTargets: minTargets,
            MaxTargets: Math.Min(maxTargets, pool.Count),
            LegalTargetIds: pool));
    }
}
