using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Resources.Interfaces;

namespace DA.Game.Domain2.Catalog.Interfaces;

public interface IEffect
{
    public ITargetingSpec Targeting { get; init; }
}
