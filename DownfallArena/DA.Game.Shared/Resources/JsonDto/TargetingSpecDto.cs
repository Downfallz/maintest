using DA.Game.Shared.Contracts.Catalog.Enums;

namespace DA.Game.Shared.Resources.JsonDto;
public sealed record TargetingSpecDto(
    TargetOrigin Origin,
    TargetScope Scope,
    int? MaxTargets = 1
);
