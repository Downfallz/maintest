using System.Text.Json;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Learning.Json;

internal sealed class MatchIdConverter : WriteOnlyConverter<MatchId>
{
    public override void Write(Utf8JsonWriter writer, MatchId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}
