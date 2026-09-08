using DA.Core.Domain.Base;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Battles.Mechanic.Abstractions
{
    public interface IStatModifierApplyer
    {
        void ApplyEffect(StatModifier effect, Character character);
    }
}