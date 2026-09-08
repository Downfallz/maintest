using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// JSON conventions for authored content and the consolidated schema: camelCase, strict about unknown members
/// so that a mistyped field is a build error rather than a silently ignored value.
/// </summary>
public static class GameSchemaJson
{
    public const string SchemaFileName = "game.schema.json";
    public const string HashFileName = "game.schema.sha256";

    public static JsonSerializerOptions ReadOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static JsonSerializerOptions WriteOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Canonical, compact form used to compute the content hash.
    /// </summary>
    public static JsonSerializerOptions HashOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };
}
