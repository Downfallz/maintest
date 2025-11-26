using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Shared.Contracts.Resources.Json;

public sealed class EffectDto
{
    public EffectKind Kind { get; init; }


    // Damage-specific
    public int? Amount { get; init; }

    // Bleed-specific
    public int? AmountPerTick { get; init; }
    public int? DurationRounds { get; init; }
}
