using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public abstract record Effect : ValueObject, IEffect {
    public TargetingSpec Targeting { get; }

    protected Effect(TargetingSpec targeting) => Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
}
