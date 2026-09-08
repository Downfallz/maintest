using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Learning.Json;

/// <summary>
/// Content ids (<c>spell:pummel:v1</c>) as their string value.
/// </summary>
internal sealed class VersionedIdConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeof(VersionedId).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert)) as JsonConverter
        ?? throw new InvalidOperationException($"No converter for '{typeToConvert.Name}'.");

    private sealed class Converter<TId> : WriteOnlyConverter<TId>
        where TId : VersionedId
    {
        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
