using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Talents.Abstractions
{
    public interface ITalentTreeStrucInitializer
    {
        TalentTreeStructure GenerateNewTree();
    }
}