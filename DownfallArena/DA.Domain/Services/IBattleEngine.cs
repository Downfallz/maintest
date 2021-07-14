using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using System.Collections.Generic;


namespace DA.Game.Domain.Services
{
    public interface IBattleEngine
    {
        Battle InitializeNewBattle();
        void StartBattle(Battle battle);
        void ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices);
        void ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices);
        void PlayAndResolveCharacterAction(Battle battle, CharacterActionChoice characterActionChoice);
    }
}