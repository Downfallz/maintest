using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A value as text with the ids a match draws for itself blanked: two matches from the same seed differ in
/// their own id and their rounds', and in nothing a player could see. What two ways of playing one match are
/// compared on.
/// </summary>
internal static partial class Normalized
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new CreatureKey() } };

    public static string Json(object? value) =>
        value is null ? "null" : DrawnId().Replace(JsonSerializer.Serialize(value, value.GetType(), Options), "id");

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
    private static partial Regex DrawnId();

    /// <summary>Writes a creature id where JSON wants text: some values key what they carry by creature.</summary>
    private sealed class CreatureKey : JsonConverter<CreatureId>
    {
        public override CreatureId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WritePropertyName(value.ToString());
    }
}
