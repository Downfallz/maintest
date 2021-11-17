using DA.AI.CharAction;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.AI
{
    public class BaseAIPlayerHandler : BasePlayerHandler
    {
        private readonly ISpeedChooser _sc;
        private readonly ISpellUnlockChooser _spellChooser;
        private readonly ICharacterActionChooser _charActionChooser;

        public BaseAIPlayerHandler(IBattleEngine battleService, ISpeedChooser sc, ISpellUnlockChooser spellChooser,
            ICharacterActionChooser charActionChooser) : base(battleService)
        {
            _sc = sc;
            _spellChooser = spellChooser;
            _charActionChooser = charActionChooser;
        }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            Character characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);

            if (characterToPlay != null)
            {
                CharacterActionChoice choices =
                    _charActionChooser.GetCharActionChoice(Battle, characterToPlay, MyAliveCharacters, MyEnemies);

                BattleEngine.PlayAndResolveCharacterAction(Battle, choices);
            }
        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            List<SpellUnlockChoice> choices = _spellChooser.GetSpellUnlockChoices(MyAliveCharacters);
            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            List<SpeedChoice> choices = _sc.GetSpeedChoices(Battle, MyAliveCharacters, MyEnemies);

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
