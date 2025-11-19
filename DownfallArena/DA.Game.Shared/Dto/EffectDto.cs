using DA.Game.Domain2.Matches.Enums;

namespace DA.Game.Resources.Dto;

public sealed class EffectDto
{
    public EffectKind Kind { get; init; }
    public TargetingSpecDto Targeting { get; init; } = default!;

    // Damage-specific
    public int? Amount { get; init; }

    // Bleed-specific
    public int? AmountPerTick { get; init; }
    public int? DurationRounds { get; init; }
}
