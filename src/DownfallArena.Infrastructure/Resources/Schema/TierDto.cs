using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TierDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public IReadOnlyList<string> Prerequisites { get; init; } = [];

    public IReadOnlyList<string> Spells { get; init; } = [];

    /// <summary>Raised on the buyer's base initiative once, and belonging to the package rather than a spell.</summary>
    public int InitiativeBonus { get; init; }

    /// <summary>
    /// Authoring-only switch (ADR 0015): <c>false</c> keeps the item out of the consolidated schema. The builder
    /// clears it, so the flag never reaches <c>game.schema.json</c> and never moves the content hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
}
