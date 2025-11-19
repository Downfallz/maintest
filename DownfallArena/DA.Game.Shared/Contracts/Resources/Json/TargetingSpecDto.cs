using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Json;
public sealed record TargetingSpecDto(
    TargetOrigin Origin,
    TargetScope Scope,
    int? MaxTargets = 1
);
