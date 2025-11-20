using System.Text.Json;
using System.Text.Json.Serialization;

namespace DA.Game.Shared.Utilities;

public static class DownfallArenaJsonOptions {
    public const string FILENAME_ALIASES = "aliases.json";
    public const string FILENAME_GAMESCHEMA = "game.schema.json";
    public const string FILENAME_SCHEMAHASH = "schema.hash";
    public const string FOLDER_SPELLS = "spells";
    public const string FOLDER_CREATURES = "creatures";
    public const string FOLDER_DATA = "Data";
    public const string FOLDER_DST = "dst";

    // Options for reading individual JSON files (spells, creatures, aliases)
    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Options for writing the final game schema (pretty-printed)
    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    // Options for hashing (stable, minified JSON)
    public static readonly JsonSerializerOptions HashOptions = new()
    {
        WriteIndented = false
    };

    static DownfallArenaJsonOptions()
    {
        // Treat enums as strings during deserialization
        ReadOptions.Converters.Add(new JsonStringEnumConverter());

        // Treat enums as strings during serialization (human-readable schema output)
        WriteOptions.Converters.Add(new JsonStringEnumConverter());

        // Treat enums as strings for hashing as well (must be consistent)
        HashOptions.Converters.Add(new JsonStringEnumConverter());
    }
}
