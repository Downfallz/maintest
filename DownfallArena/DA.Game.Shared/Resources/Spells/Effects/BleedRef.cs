namespace DA.Game.Shared.Resources.Spells.Effects;

public sealed record BleedRef : IEffect, IOverTimeEffect
{
    public required ITargetingSpec Targeting { get; init; }
    public int AmountPerTick { get; }
    public int DurationRounds { get; }
}
