using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public interface IEffect
{
    public EffectKind Kind { get; }
    public TargetingSpec Targeting { get; }
}
