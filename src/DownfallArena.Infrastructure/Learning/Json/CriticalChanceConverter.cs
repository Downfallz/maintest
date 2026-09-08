using System.Text.Json;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Infrastructure.Learning.Json;

internal sealed class CriticalChanceConverter : WriteOnlyConverter<CriticalChance>
{
    public override void Write(Utf8JsonWriter writer, CriticalChance value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}
