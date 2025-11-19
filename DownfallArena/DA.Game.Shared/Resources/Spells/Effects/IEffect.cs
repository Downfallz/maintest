namespace DA.Game.Shared.Resources.Spells.Effects;

public interface IEffect
{
    public ITargetingSpec Targeting { get; init; }
}
