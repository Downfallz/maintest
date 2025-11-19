using DA.Game.Resources.Enums;

namespace DA.Game.Resources.Dto;
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