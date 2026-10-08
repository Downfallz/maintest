using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TierDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public IReadOnlyList<string> Prerequisites { get; init; } = [];

    /// <summary>
    /// Packages of which the buyer must own at least one (ADR 0101), absent rather than empty on every package that
    /// names none, so a catalogue authored before it keeps its content hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AnyOf { get; init; }

    public IReadOnlyList<string> Spells { get; init; } = [];

    /// <summary>Raised on the buyer's base initiative once, and belonging to the package rather than a spell.</summary>
    public int InitiativeBonus { get; init; }

    /// <summary>What the package gives its owner for good (ADR 0101), absent on every package that gives nothing through it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PassiveDto? Passive { get; init; }

    /// <summary>Whether the package uses a member a reader written before ADR 0101 does not know.</summary>
    [JsonIgnore]
    public bool UsesCapstoneMembers => AnyOf is { Count: > 0 } || Passive is not null;

    /// <summary>
    /// Authoring-only switch (ADR 0015): <c>false</c> keeps the item out of the consolidated schema. The builder
    /// clears it, so the flag never reaches <c>game.schema.json</c> and never moves the content hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
}
