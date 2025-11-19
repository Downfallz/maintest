namespace DA.Game.Shared.Resources.Spells.Effects;

public sealed record DamageRef : IEffect, IInstantEffect
{
    public required ITargetingSpec Targeting { get; init; }
    public int Amount { get; init; }
}
