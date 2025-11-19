using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells
{
    public interface ITargetingSpec
    {
        int? MaxTargets { get; init; }
        TargetOrigin Origin { get; init; }
        TargetScope Scope { get; init; }
    }
}