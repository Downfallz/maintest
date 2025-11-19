using DA.Game.Domain2.Catalog.Interfaces;
using DA.Game.Resources.Interfaces;

namespace DA.Game.Domain2.Matches.Resources.Effects;

public sealed record DamageRef : IEffect, IInstantEffect
{
    public required ITargetingSpec Targeting { get; init; }
    public int Amount { get; init; }
}
