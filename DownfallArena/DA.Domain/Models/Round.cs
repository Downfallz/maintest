using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.Enum;

namespace DA.Game.Domain.Models
{
    [Serializable]
    public class Round
    {
        public Round()
        {
            PlayerOneSpeedChoice = new List<SpeedChoice>();
            PlayerTwoSpeedChoice = new List<SpeedChoice>();
            PlayerOneSpellUnlocks = new List<SpellUnlockChoice>();
            PlayerTwoSpellUnlocks = new List<SpellUnlockChoice>();
            OrderedCharacters = new List<Character>();
            CharacterActionChoices = new List<CharacterActionChoice>();
        }
        public List<SpellUnlockChoice> PlayerOneSpellUnlocks { get; set; }
        public List<SpellUnlockChoice> PlayerTwoSpellUnlocks { get; set; }
        public List<SpeedChoice> PlayerOneSpeedChoice { get; set; }
        public List<SpeedChoice> PlayerTwoSpeedChoice { get; set; }
        public List<Character> OrderedCharacters { get; set; }
        public List<CharacterActionChoice> CharacterActionChoices { get; set; }

        public List<SpeedChoice> AllSpeedChoice => PlayerOneSpeedChoice.Concat(PlayerTwoSpeedChoice).ToList();
        public int? CurrentCharacterIndex { get; set; }
        public RoundStatus RoundStatus { get; set; }
    }
}
