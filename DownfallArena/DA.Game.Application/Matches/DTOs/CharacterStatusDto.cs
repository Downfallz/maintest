using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CharacterStatusDto(
    CharacterId CharacterId,
    int Health,
    int Energy,
    int Initiative,
    bool IsStunned,
    bool IsAlive,
    int BaseHealth,
    int BaseEnergy,
    int BaseDefense,
    int BaseInitiative,
    double BaseCritical
);