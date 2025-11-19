namespace DA.Game.Shared.Contracts.Resources.Spells.Effects;

public interface IEffect
{
    public ITargetingSpec Targeting { get; init; }
}
