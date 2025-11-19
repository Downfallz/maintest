using DA.Game.Shared.Contracts.Catalog.Enums;

namespace DA.Game.Shared.Resources.JsonDto;
public sealed record CreatureDefinitionDto(
    string Id,
    string Name,
    CharClass CharacterClass,   // ex: "Brawler"
    int BaseHealth,
    int BaseEnergy,
    int BaseDefense,
    int BaseInitiative,
    double BaseCriticalChance, // 0.15 = 15%
    string[] StartingSpellIds
);