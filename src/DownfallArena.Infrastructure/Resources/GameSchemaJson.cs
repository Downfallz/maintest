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
    /// What a loose read of the document allows, matching <see cref="ReadOptions"/> where it can: the version
    /// is read from the same bytes the strict reader will read, so the two must not disagree about what parses.
    /// </summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Canonical, compact form used to compute the content hash.
    /// </summary>
    public static JsonSerializerOptions HashOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };
}
