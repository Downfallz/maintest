using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Learning.Json;

/// <summary>
/// Values declared as one of the given abstract types or interfaces are written as their concrete type, with a
/// leading <c>kind</c> holding the type name. Concrete types are left to the default serialization, which is what
/// keeps this from recursing.
/// </summary>
internal sealed class KindedObjectConverterFactory(params Type[] baseTypes) : JsonConverterFactory
{
    public const string KindProperty = "kind";

    public override bool CanConvert(Type typeToConvert) =>
        (typeToConvert.IsAbstract || typeToConvert.IsInterface) && Array.Exists(baseTypes, baseType => baseType.IsAssignableFrom(typeToConvert));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert)) as JsonConverter
        ?? throw new InvalidOperationException($"No converter for '{typeToConvert.Name}'.");

    private sealed class Converter<TBase> : WriteOnlyConverter<TBase>
        where TBase : class
    {
        public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
        {
            var type = value.GetType();
            var node = JsonSerializer.SerializeToNode(value, type, options) as JsonObject
                ?? throw new JsonException($"'{type.Name}' did not serialize as an object.");
            node.Insert(0, KindProperty, type.Name);
            node.WriteTo(writer, options);
        }
    }
}
