using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.Enum;

namespace DA.Game.Domain.Services
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