using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Json;

public sealed record CreatureDefinitionDto(
    string Id,
    string Name,
    CreatureClass CreatureClass,       // ex: "Brawler", "Sorcerer", "Beast", "Boss", etc.
    int BaseHealth,
    int BaseEnergy,
    int BaseDefense,
    int BaseInitiative,
    double BaseCriticalChance,         // 0.15 = 15%

    string? TalentTreeId,              // <--- nouvel ajout (nullable)
    IReadOnlyList<string> StartingSpellIds
);
