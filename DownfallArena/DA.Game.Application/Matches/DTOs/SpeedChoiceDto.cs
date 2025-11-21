using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.DTOs;

public sealed record SpeedChoiceDto(
    CharacterId CharacterId,
    Speed Speed
);
