using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;

namespace DA.Game.Domain.Services.GameFlowEngine
{
    public interface IRoundService
    {
        void InitializeNewRound(Battle battle);
        void ApplySpellsToUnlock(Battle battle);
        void ResolveCharacterOrder(Battle battle);
        Guid? GetCurrentCharacterIdActionTurn(Round battle);
        void AssignNextCharacter(Round round);

        bool ChooseTeamSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices);
        bool ChooseTeamSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices);
        void PlayAndResolveCharacterAction(Round round, CharacterActionChoice characterActionChoice);
    }
}