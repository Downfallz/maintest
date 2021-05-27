using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Talents.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Teams.Abstractions;
using DA.Core.Teams.TalentTree;

namespace DA.Core.Teams
{
    public class CharacterDevelopmentService : ICharacterDevelopmentService
    {
        private readonly ICharacterTalentStatsHandler _characterTalentTreeService;
        private readonly ITalentTreeManager _talentTreeService;

        public CharacterDevelopmentService(ICharacterTalentStatsHandler characterTalentTreeService, ITalentTreeManager talentTreeService)
        {
            _characterTalentTreeService = characterTalentTreeService;
            _talentTreeService = talentTreeService;
        }

        public Character InitializeNewCharacter()
        {
            var newChar = new Character();
            newChar.TalentTreeStructure = _talentTreeService.InitializeNewTalentTree();
            newChar.CharacterTalentStats = _characterTalentTreeService.UpdateCharTalentTree(newChar.TalentTreeStructure);
            newChar.BaseHealth = 20;
            InitCharState(newChar);

            return newChar;
        }

        private void InitCharState(Character newChar)
        {
            newChar.Health = newChar.BaseHealth;
            newChar.BonusDefense = 0;
            newChar.BonusAttackPower = 0;
            newChar.Energy = 0;
        }

        public void UnlockSpell(Character character, Spell spell)
        {
            var stats = _characterTalentTreeService.UnlockSpell(character.TalentTreeStructure, spell);
            character.CharacterTalentStats = stats;
        }
    }
}
