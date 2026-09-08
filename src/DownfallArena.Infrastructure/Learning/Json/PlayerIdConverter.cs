using System.Text.Json;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Learning.Json;

internal sealed class PlayerIdConverter : WriteOnlyConverter<PlayerId>
{
    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}
