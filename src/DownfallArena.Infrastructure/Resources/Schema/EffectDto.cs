namespace DownfallArena.Infrastructure.Resources.Schema;

/// <summary>
/// Flat representation of any effect kind; the mapper checks which fields each kind requires (see data/README.md).
/// </summary>
public sealed record EffectDto
{
    public string Kind { get; init; } = string.Empty;

    public int? Amount { get; init; }

    public int? AmountPerRound { get; init; }

    public int? DurationRounds { get; init; }

    public bool Permanent { get; init; }

    public string? Stacking { get; init; }
}
