namespace DA.Game.Domain2.Catalog.ValueObjects.Spells;

public interface IEffect
{
    public ITargetingSpec Targeting { get; init; }
}
