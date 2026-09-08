namespace DA.Game.Shared.Contracts.Resources.Json;

// A spell entry inside a node, with its own prerequisites
public sealed record TalentTreeSpellNodeDto(
    string Id,
    TalentPrerequisitesDto Prerequisites
);
