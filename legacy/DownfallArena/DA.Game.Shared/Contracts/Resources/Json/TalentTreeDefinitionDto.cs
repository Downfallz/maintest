namespace DA.Game.Shared.Contracts.Resources.Json;

// Root DTO for a talent tree JSON definition
public sealed record TalentTreeDefinitionDto(
    string Id,
    string Name,
    TalentTreeNodeDto Root
);
