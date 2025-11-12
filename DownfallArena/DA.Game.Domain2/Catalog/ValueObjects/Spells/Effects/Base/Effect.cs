using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells.Effects.Base;

public abstract record Effect : ValueObject
{
    public TargetingSpec Targeting { get; init; }

    protected Effect(TargetingSpec targeting) => Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
}
