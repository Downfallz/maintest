using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

public sealed class InstantEffectService(IDamageComputationService damageComputationService) : IInstantEffectService
{
    public void ApplyInstantEffect(InstantEffectApplication eff,  CombatCreature target)
    {
        ArgumentNullException.ThrowIfNull(eff);
        ArgumentNullException.ThrowIfNull(target);

        switch (eff.Kind)
        {
            case EffectKind.Damage:
                ApplyDamage(target, eff);
                break;
            // Other kinds (Heal, Energy, Buff, etc.) will be implemented later.
            default:
                break;
        }
    }

    private void ApplyDamage(CombatCreature target, InstantEffectApplication eff)
    {
        var defender = CreatureSnapshot.From(target);

        var finalDamage = damageComputationService.ComputeFinalDamage(
            eff.Amount,
            defender
        );

        if (finalDamage > 0)
            target.TakeDamage(finalDamage);
    }
}
