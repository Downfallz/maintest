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
using System.Threading.Tasks;
using DA.Game.Domain;

namespace DA.AI
{
    public class BaseAIPlayerHandler : BasePlayerHandler
    {
        private readonly ISpeedChooser _sc;
        private readonly ISpellUnlockChooser _spellChooser;
        private readonly ICharacterActionChooser _charActionChooser;

        public BaseAIPlayerHandler(IBattleController battleService, ISpeedChooser sc, ISpellUnlockChooser spellChooser,
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
                CharacterActionChoice choice = null;
                if (!characterToPlay.IsDead && !characterToPlay.IsStunned) 
                    choice = _charActionChooser.GetCharActionChoice(Battle, characterToPlay, MyAliveCharacters, MyEnemies).Result;

                BattleEngine.PlayAndResolveCharacterAction(Battle, choice);
            }
        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            List<SpellUnlockChoice> choices = _spellChooser.GetSpellUnlockChoices(Battle, MyAliveCharacters, MyEnemies);
            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            List<SpeedChoice> choices = _sc.GetSpeedChoices(Battle, MyAliveCharacters, MyEnemies);

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
