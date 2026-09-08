using DA.Game.Domain2.Matches.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.ActionResolution.Execution;

public sealed class ConditionEffectServiceV1 : IConditionEffectService
{
    public void ApplyCondition(ConditionApplication application, CombatCreature target)
    {
        // Central place for future invariants (ex: dead can't receive conditions, stun no-stack, etc.)
        target.AddCondition(application.Condition);
    }
}