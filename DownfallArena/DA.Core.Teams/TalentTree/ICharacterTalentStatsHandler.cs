using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Teams.TalentTree
{
    public interface ICharacterTalentStatsHandler
    {
        TalentTreeStructure InitializeCharacterTalentTree();
        CharacterTalentStats UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell);
        CharacterTalentStats UpdateCharTalentTree(TalentTreeStructure talentTreeStructure);
    }
}