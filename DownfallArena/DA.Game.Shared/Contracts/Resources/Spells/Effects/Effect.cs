using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public abstract record Effect : ValueObject, IEffect
{
    public ITargetingSpec Targeting { get; init; }

    protected Effect(ITargetingSpec targeting) => Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
}
