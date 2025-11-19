using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Shared.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatActionChoiceDto(
    CharacterId ActorId,
    SpellRef SpellRef,
    IReadOnlyList<CharacterId> TargetIds
);