using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.CombatMechanic.Tools
{
    public interface IStatModifierApplyer
    {
        void ApplyEffect(StatModifier effect, Character character);
    }
}