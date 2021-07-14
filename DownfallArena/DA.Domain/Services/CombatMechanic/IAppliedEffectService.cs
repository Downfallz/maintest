using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using System.Collections.Generic;

namespace DA.Game.Domain.Services.GameFlowEngine.CombatMechanic
{
    public interface IAppliedEffectService
    {
        void ApplyEffect(AppliedEffect effect, Character source, List<Character> targets);
    }
}