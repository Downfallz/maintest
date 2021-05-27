using System;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Services.GameFlowEngine.TalentsManagement;
using DA.Game.TalentsManagement.Tools;

namespace DA.Game.TalentsManagement
{
    public class CharacterDevelopmentService : ICharacterDevelopmentService
    {
        private readonly ICharacterTalentStatsHandler _characterTalentStatsHandler;
        private readonly ITalentTreeManager _talentTreeService;

        public CharacterDevelopmentService(ICharacterTalentStatsHandler characterTalentStatsHandler, ITalentTreeManager talentTreeService)
        {
            _characterTalentStatsHandler = characterTalentStatsHandler;
            _talentTreeService = talentTreeService;
        }

        public Character InitializeNewCharacter()
        {
            var newChar = new Character();
            newChar.TalentTreeStructure = _talentTreeService.InitializeNewTalentTree();
            newChar.CharacterTalentStats = _characterTalentStatsHandler.UpdateCharTalentTree(newChar.TalentTreeStructure);
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
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));
            if(character.TalentTreeStructure == null)
                throw new ArgumentException("Character tree structure can't be null", nameof(character));

            var stats = _characterTalentStatsHandler.UnlockSpell(character.TalentTreeStructure, spell);
            character.CharacterTalentStats = stats;
        }
    }
}
