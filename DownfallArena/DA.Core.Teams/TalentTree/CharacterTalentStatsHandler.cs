using System.Linq;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Teams.TalentTree
{
    public class CharacterTalentStatsHandler : ICharacterTalentStatsHandler
    {

        private readonly ITalentTreeManager _talentTreeService;

        public CharacterTalentStatsHandler(ITalentTreeManager talentTreeService)
        {
            _talentTreeService = talentTreeService;
        }

        public TalentTreeStructure InitializeCharacterTalentTree()
        {
            return _talentTreeService.InitializeNewTalentTree();
        }

        public CharacterTalentStats UnlockSpell(TalentTreeStructure talentTreeStructure, Spell spell)
        {
            _talentTreeService.UnlockSpell(talentTreeStructure, spell);

            return UpdateCharTalentTree(talentTreeStructure);
        }

        public CharacterTalentStats UpdateCharTalentTree(TalentTreeStructure talentTreeStructure)
        {
            var stats = new CharacterTalentStats();

            var unlockedTalents = _talentTreeService.GetUnlockedTalents(talentTreeStructure);
            var unlockableTalents = _talentTreeService.GetUnlockableTalents(talentTreeStructure);
            stats.UnlockableTalents = unlockableTalents;
            stats.UnlockedTalents = unlockedTalents;

            stats.Spells = unlockedTalents.Select(x => x).ToList();
            stats.PassiveEffects = unlockedTalents.SelectMany(x => x.PassiveEffects).ToList();
            stats.Initiative = unlockedTalents.Sum(x => x.Initiative);
            return stats;
        }
    }
}
