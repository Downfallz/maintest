using System.Text.Json;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Learning.Json;

internal sealed class RoundIdConverter : WriteOnlyConverter<RoundId>
{
    public override void Write(Utf8JsonWriter writer, RoundId value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Number);
}
