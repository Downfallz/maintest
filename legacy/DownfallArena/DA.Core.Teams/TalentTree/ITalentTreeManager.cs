using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;

namespace DA.Core.Teams.TalentTree
{
    public interface ITalentTreeManager
    {
        TalentTreeStructure InitializeNewTalentTree();
        IReadOnlyList<Spell> GetUnlockedTalents(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Spell> GetUnlockableTalents(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Spell> GetAllTalents(TalentTreeStructure talentTreeStructure);
        bool UnlockSpell(TalentTreeStructure talentTreeStructure, Spell talent);
    }
}