using DA.Game.Domain2.Matches.Resources; // pour SpellRef
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Application.Matches.DTOs;

public sealed record SpellUnlockChoiceDto(
    CharacterId CharacterId,
    SpellRef SpellRef
);
