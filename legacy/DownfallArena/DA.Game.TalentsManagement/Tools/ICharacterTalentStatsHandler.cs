using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public interface ICharacterTalentStatsHandler
    {
        TalentTreeStructure InitializeCharacterTalentTree();
        CharacterTalentStats UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell);
        CharacterTalentStats UpdateCharTalentTree(TalentTreeStructure talentTreeStructure);
    }
}