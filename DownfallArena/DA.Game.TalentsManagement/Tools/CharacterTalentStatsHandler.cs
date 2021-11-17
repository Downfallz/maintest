using System;
using System.Linq;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public class CharacterTalentStatsHandler : ICharacterTalentStatsHandler
    {

        private readonly ITalentTreeManager _talentTreeManager;

        public CharacterTalentStatsHandler(ITalentTreeManager talentTreeManager)
        {
            _talentTreeManager = talentTreeManager;
        }

        public TalentTreeStructure InitializeCharacterTalentTree()
        {
            return _talentTreeManager.InitializeNewTalentTree();
        }

        public CharacterTalentStats UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell)
        {
            if (talentTreeStructure == null)
                throw new ArgumentNullException(nameof(talentTreeStructure));
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));
            if (talentTreeStructure.Root == null)
                throw new ArgumentException("Talent tree structure root can't be null", nameof(talentTreeStructure));

            _talentTreeManager.UnlockSpell(talentTreeStructure, spell);

            return UpdateCharTalentTree(talentTreeStructure);
        }

        public CharacterTalentStats UpdateCharTalentTree(TalentTreeStructure talentTreeStructure)
        {
            if (talentTreeStructure == null)
                throw new ArgumentNullException(nameof(talentTreeStructure));
            if (talentTreeStructure.Root == null)
                throw new ArgumentException("Talent tree structure root can't be null", nameof(talentTreeStructure));

            CharacterTalentStats stats = new CharacterTalentStats();

            System.Collections.Generic.IReadOnlyList<Spell> unlockedSpells = _talentTreeManager.GetUnlockedSpells(talentTreeStructure);
            System.Collections.Generic.IReadOnlyList<Spell> unlockableSpells = _talentTreeManager.GetUnlockableSpells(talentTreeStructure);
            stats.UnlockableSpells = unlockableSpells;
            stats.UnlockedSpells = unlockedSpells;

            stats.PassiveEffects = unlockedSpells.SelectMany(x => x.PassiveEffects).ToList();
            stats.Initiative = unlockedSpells.Sum(x => x.Initiative);
            return stats;
        }
    }
}
