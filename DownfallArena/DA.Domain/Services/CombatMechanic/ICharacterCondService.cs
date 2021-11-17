using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.Game.Domain.Services.CombatMechanic
{
    public interface ICharacterCondService
    {
        void ApplyCondition(CharCondition charCond, Character target);
    }
}