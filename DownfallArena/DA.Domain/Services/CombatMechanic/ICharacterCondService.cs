using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.Game.Domain.Services.CombatMechanic
{
    public interface ICharacterCondService
    {
        CharCondApplyResult ApplyCondition(CharCondition charCond, Character target);
    }
}