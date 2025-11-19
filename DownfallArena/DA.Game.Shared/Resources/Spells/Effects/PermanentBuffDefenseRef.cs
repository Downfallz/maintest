namespace DA.Game.Shared.Resources.Spells.Effects;

public sealed record PermanentBuffDefenseRef : IEffect, IInstantEffect
{
    public required ITargetingSpec Targeting { get; init; }
    public int Amount { get; }
}
