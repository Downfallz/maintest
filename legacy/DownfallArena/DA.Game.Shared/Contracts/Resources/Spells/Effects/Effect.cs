using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public abstract record Effect : ValueObject, IEffect
{
    public EffectKind Kind { get; }

    protected Effect(EffectKind Kind) { }
}
