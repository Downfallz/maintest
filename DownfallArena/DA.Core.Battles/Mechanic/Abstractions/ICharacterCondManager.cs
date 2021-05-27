using DA.Core.Domain.Base.Teams;

namespace DA.Core.Battles.Mechanic.Abstractions
{
    public interface ICharacterCondManager
    {
        void ApplyCondition(CharCondition charCond, Character target);
    }
}