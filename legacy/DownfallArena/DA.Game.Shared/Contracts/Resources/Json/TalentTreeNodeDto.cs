namespace DA.Game.Shared.Contracts.Resources.Json;

// A node in the talent tree hierarchy (BaseCreature, Brawler, Warlord, etc.)
public sealed record TalentTreeNodeDto(
    string Code,
    string Name,
    TalentPrerequisitesDto Prerequisites,
    IReadOnlyList<TalentTreeSpellNodeDto> Spells,
    IReadOnlyList<TalentTreeNodeDto> Children
);
