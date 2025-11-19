using DA.Game.Domain2.Shared.Resources.Enums;

namespace DA.Game.Resources.Dto;
public sealed record TargetingSpecDto(
    TargetOrigin Origin,
    TargetScope Scope,
    int? MaxTargets = 1
);
