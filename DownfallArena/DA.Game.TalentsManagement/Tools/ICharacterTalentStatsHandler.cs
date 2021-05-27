using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public interface ICharacterTalentStatsHandler
    {
        TalentTreeStructure InitializeCharacterTalentTree();
        CharacterTalentStats UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell);
        CharacterTalentStats UpdateCharTalentTree(TalentTreeStructure talentTreeStructure);
    }
}