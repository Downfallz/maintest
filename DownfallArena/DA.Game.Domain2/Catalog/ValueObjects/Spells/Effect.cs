using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Resources.Spells.Effects;

namespace DA.Game.Domain2.Catalog.ValueObjects.Spells;

public abstract record Effect : ValueObject, IEffect
{
    public ITargetingSpec Targeting { get; init; }

    protected Effect(ITargetingSpec targeting) => Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
}
