using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public interface ITalentTreeManager
    {
        TalentTreeStructure InitializeNewTalentTree();
        IReadOnlyList<Spell> GetUnlockedSpells(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Spell> GetUnlockableSpells(TalentTreeStructure talentTreeStructure);
        IReadOnlyList<Spell> GetAllSpells(TalentTreeStructure talentTreeStructure);
        bool UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell);
    }
}