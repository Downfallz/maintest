using DA.Game.Shared.Contracts.Catalog.Enums;

namespace DA.Game.Shared.Resources.Spells.Effects
{
    public interface ITargetingSpec
    {
        int? MaxTargets { get; init; }
        TargetOrigin Origin { get; init; }
        TargetScope Scope { get; init; }
    }
}