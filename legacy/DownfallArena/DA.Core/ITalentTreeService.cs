using System.Collections.Generic;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Talents
{
    public interface ITalentTreeService
    {
        TalentTreeStructure InitializeNewTalentTree();
        IReadOnlyList<Talent> GetUnlockedTalents(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Talent> GetUnlockableTalents(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Talent> GetAllTalents(TalentTreeStructure talentTreeStructure);
        bool UnlockTalent(TalentTreeStructure talentTreeStructure, Talent talent);
    }
}