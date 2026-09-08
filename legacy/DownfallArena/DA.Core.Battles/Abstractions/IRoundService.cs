using System;
using System.Collections.Generic;
using DA.Core.Domain.Base.Teams.Enum;
using DA.Core.Domain.Battles;

namespace DA.Core.Battles.Abstractions
{
    public interface IRoundService
    {
        void InitializeNewRound(Battle battle);
        bool ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices);
        bool ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices);
        void ApplySpellsToUnlock(Battle battle);
        void ResolveCharacterOrder(Battle battle);
        Guid? GetCurrentCharacterId(Round battle);
        void PlayAndResolveCharacterAction(Round round, CharacterActionChoice characterActionChoice);

    }
}