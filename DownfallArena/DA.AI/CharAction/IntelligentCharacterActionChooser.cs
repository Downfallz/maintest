using DA.AI.CharAction.Tgt;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Threading.Tasks;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;

namespace DA.AI.CharAction
{
    public class IntelligentCharacterActionChooser : ICharacterActionChooser
    {
        private readonly IBestCharacterActionChoicePicker _bestCharacterActionChoicePicker;

        public IntelligentCharacterActionChooser(IBestCharacterActionChoicePicker bestCharacterActionChoicePicker)
        {
            _bestCharacterActionChoicePicker = bestCharacterActionChoicePicker;
        }

        public async Task<CharacterActionChoice> GetCharActionChoice(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            var possibleSpell = charToPlay.CharacterTalentStats.UnlockedSpells
                .Where(x => x.EnergyCost <= charToPlay.Energy && (!x.MinionsCost.HasValue || x.MinionsCost.Value <= charToPlay.ExtraPoint)).ToList();

            BestPick bestPick = await _bestCharacterActionChoicePicker.GetCharActionBestPick(battle, charToPlay, aliveCharacters, aliveEnemies);

            var choice = new CharacterActionChoice()
            {
                CharacterId = charToPlay.Id,
                Spell = bestPick.Spell,
                Targets = bestPick.BestTargets
            };

            return choice;
        }
    }
}
