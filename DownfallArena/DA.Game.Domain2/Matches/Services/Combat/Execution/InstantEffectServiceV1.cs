using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat;

public sealed class InstantEffectServiceV1(IDamageComputationService damageComputationService) : IInstantEffectService
{
    public void ApplyInstantEffect(Match match, CombatCreature target, InstantEffectApplication eff)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(eff);
        switch (eff.Kind)
        {
            case EffectKind.Damage:
                ApplyDamage(target, eff);
                break;

        }
    }

    private void ApplyDamage(CombatCreature target, InstantEffectApplication eff)
    {
        var attacker = CharacterStatus.From(target);
        var defender = CharacterStatus.From(target);

        var finalDamage = damageComputationService.ComputeFinalDamage(
            eff.Amount,
            attacker,
            defender
        );

        if (finalDamage > 0)
            target.TakeDamage(finalDamage);
    }
}
