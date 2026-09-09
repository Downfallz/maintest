using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TalentTreeDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public TalentNodeDto? Root { get; init; }

    /// <summary>
    /// Authoring-only switch (ADR 0015): <c>false</c> keeps the item out of the consolidated schema. The builder
    /// clears it, so the flag never reaches <c>game.schema.json</c> and never moves the content hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
}
