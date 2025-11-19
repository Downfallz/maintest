using DA.Game.Domain2.Catalog.Interfaces;
using DA.Game.Resources.Interfaces;

namespace DA.Game.Domain2.Matches.Resources.Effects;

public sealed record BleedRef : IEffect, IOverTimeEffect
{
    public required ITargetingSpec Targeting { get; init; }
    public int AmountPerTick { get; }
    public int DurationRounds { get; }
}
