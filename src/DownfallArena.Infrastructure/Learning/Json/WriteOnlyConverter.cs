using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Learning.Json;

/// <summary>
/// Base for the artifact converters: the engine writes artifacts and never reads them back (ADR 0013).
/// </summary>
internal abstract class WriteOnlyConverter<TValue> : JsonConverter<TValue>
{
    public override TValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"Artifacts are write-only; '{typeToConvert.Name}' is not read back by the engine.");
}
