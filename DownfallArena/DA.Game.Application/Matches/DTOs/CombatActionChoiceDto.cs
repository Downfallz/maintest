using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatActionChoiceDto(
    CharacterId ActorId,
    Spell SpellRef,
    IReadOnlyList<CharacterId> TargetIds
);