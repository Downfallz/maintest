using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.CombatMechanic.Tools
{
    public interface IStatModifierApplyer
    {
        void ApplyEffect(StatModifier effect, Character character);
    }
}