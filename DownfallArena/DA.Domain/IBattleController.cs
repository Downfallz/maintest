using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.Enum;

namespace DA.Game.Domain
{
    public interface IBattleController
    {
        Battle InitializeNewBattle();
        void StartBattle(Battle battle);
        void ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices);
        void ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices);
        void PlayAndResolveCharacterAction(Battle battle, CharacterActionChoice characterActionChoice);
    }
}