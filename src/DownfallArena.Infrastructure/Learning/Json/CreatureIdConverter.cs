using System.Globalization;
using System.Text.Json;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Learning.Json;

/// <summary>
/// Creature ids as numbers, and as property names when they key a dictionary.
/// </summary>
internal sealed class CreatureIdConverter : WriteOnlyConverter<CreatureId>
{
    public override void Write(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.ToString(CultureInfo.InvariantCulture));
}
