using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;

namespace DA.Game.Domain.Services.GameFlowEngine.CombatMechanic
{
    public interface ICharacterCondService
    {
        void ApplyCondition(CharCondition charCond, Character target);
    }
}