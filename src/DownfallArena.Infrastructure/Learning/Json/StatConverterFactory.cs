using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Infrastructure.Learning.Json;

/// <summary>
/// Health, energy, defense, and initiative as plain numbers.
/// </summary>
internal sealed class StatConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.BaseType is { IsGenericType: true } baseType && baseType.GetGenericTypeDefinition() == typeof(NonNegativeStat<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert)) as JsonConverter
        ?? throw new InvalidOperationException($"No converter for '{typeToConvert.Name}'.");

    private sealed class Converter<TStat> : WriteOnlyConverter<TStat>
        where TStat : NonNegativeStat<TStat>
    {
        public override void Write(Utf8JsonWriter writer, TStat value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
    }
}
