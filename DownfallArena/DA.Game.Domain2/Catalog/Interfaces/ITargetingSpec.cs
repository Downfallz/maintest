using DA.Game.Domain2.Catalog.ValueObjects.Spells;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Domain2.Shared.Resources.Enums;

namespace DA.Game.Resources.Interfaces
{
    public interface ITargetingSpec
    {
        int? MaxTargets { get; init; }
        TargetOrigin Origin { get; init; }
        TargetScope Scope { get; init; }

        static abstract TargetingSpec Of(TargetOrigin origin, TargetScope scope, int? maxTargets = 1);
        void Deconstruct(out TargetOrigin Origin, out TargetScope Scope, out int? MaxTargets);
        bool Equals(object? obj);
        bool Equals(TargetingSpec? other);
        bool Equals(ValueObject? other);
        int GetHashCode();
        string ToString();
    }
}