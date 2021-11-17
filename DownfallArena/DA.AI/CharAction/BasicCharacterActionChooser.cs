using DA.AI.CharAction.Tgt;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using System;
using System.Collections.Generic;

namespace DA.AI.CharAction
{
    public class BasicCharacterActionChooser : ICharacterActionChooser
    {
        private readonly ISpellChooser _spellChooser;
        private readonly ITargetChooser _targetChooser;

        public BasicCharacterActionChooser(ISpellChooser spellChooser, ITargetChooser targetChooser)
        {
            _spellChooser = spellChooser;
            _targetChooser = targetChooser;
        }

        public CharacterActionChoice GetCharActionChoice(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            var spell = _spellChooser.ChooseSpell(charToPlay);
            List<Guid> targets = _targetChooser.ChooseTargetForSpell(spell, aliveCharacters, aliveEnemies);

            var choice = new CharacterActionChoice()
            {
                CharacterId = charToPlay.Id,
                Spell = spell,
                Targets = targets
            };

            return choice;
        }
    }
}
