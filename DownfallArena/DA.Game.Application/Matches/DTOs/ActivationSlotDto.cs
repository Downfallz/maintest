using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.Enums; // PlayerSlot, Speed

public sealed record ActivationSlotDto(
    PlayerSlot PlayerSlot,
    CombatCharacterDto CombatCharacter,
    Speed Speed,
    int InitiativeValue
);
