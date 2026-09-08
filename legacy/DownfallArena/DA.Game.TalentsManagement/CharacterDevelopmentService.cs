using DA.Game.TalentsManagement.Tools;
using System;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services.TalentsManagement;

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
            Character newChar = new Character
            {
                TalentTreeStructure = _talentTreeService.InitializeNewTalentTree()
            };
            newChar.CharacterTalentStats = _characterTalentStatsHandler.UpdateCharTalentTree(newChar.TalentTreeStructure);
            newChar.BaseHealth = 20;
            InitCharState(newChar);

            return newChar;
        }

        private void InitCharState(Character newChar)
        {
            newChar.Health = newChar.BaseHealth;
            newChar.BonusDefense = 0;
            newChar.Energy = 0;
        }

        public void UnlockSpell(Character character, Spell spell)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));
            if (character.TalentTreeStructure == null)
                throw new ArgumentException("Character tree structure can't be null", nameof(character));

            CharacterTalentStats stats = _characterTalentStatsHandler.UnlockSpell(character.TalentTreeStructure, spell);
            character.CharacterTalentStats = stats;
        }
    }
}
