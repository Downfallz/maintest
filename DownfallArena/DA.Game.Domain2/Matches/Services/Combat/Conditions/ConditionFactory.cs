using DA.Game.Domain2.Matches.Entities.Conditions;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Conditions;

public static class ConditionFactory
{
    public static ConditionInstance FromOverTimeEffect(Effect effect)
        => effect switch
        {
            Bleed b => new ConditionInstance
            {
                Kind = ConditionKind.DamageOverTime,
                StackPolicy = StackPolicy.Stack,
                Phase = ConditionPhase.StartOfRound,
                Modifier = b.AmountPerTick,
                RemainingRounds = b.DurationRounds
            },

            _ => throw new NotSupportedException($"Unsupported overtime effect: {effect.Kind}")
        };
}