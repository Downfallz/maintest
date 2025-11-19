using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Shared.Resources.JsonDto;

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
