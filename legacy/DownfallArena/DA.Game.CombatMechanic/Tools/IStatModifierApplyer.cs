using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services.CombatMechanic;

namespace DA.Game.CombatMechanic.Tools
{
    public interface IStatModifierApplyer
    {
        StatModifierResult ApplyEffect(StatModifier effect, Character character);
    }
}