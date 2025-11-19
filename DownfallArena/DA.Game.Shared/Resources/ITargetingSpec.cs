using DA.Game.Domain2.Shared.Resources.Enums;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells
{
    public interface ITargetingSpec
    {
        int? MaxTargets { get; init; }
        TargetOrigin Origin { get; init; }
        TargetScope Scope { get; init; }
    }
}