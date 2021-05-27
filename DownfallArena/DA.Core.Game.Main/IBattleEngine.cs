using System;
using System.Collections.Generic;
using DA.Core.Domain.Base.Teams.Enum;
using DA.Core.Domain.Battles;
using DA.Core.Game.Main.Events;

namespace DA.Core.Game.Main
{
    public interface IBattleEngine
    {
        event EventHandler NewRoundInitialized;
        event EventHandler AllSpellUnlocked;
        event EventHandler AllSpeedChosen;
        event EventHandler<CharacterTurnInitializedEventArgs> CharacterTurnInitialized;

        Battle InitializeNewBattle();
        void StartBattle(Battle battle);
        void ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices);
        void ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices);
        void PlayAndResolveCharacterAction(Battle battle, CharacterActionChoice characterActionChoice);
    }
}