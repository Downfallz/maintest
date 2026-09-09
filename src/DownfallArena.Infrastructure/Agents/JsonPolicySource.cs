using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;

namespace DownfallArena.Infrastructure.Agents;

/// <summary>
/// Reads a <c>policy.json</c> written by the Python side (docs/learning/training.md): plain values, camelCase,
/// the fields the engine needs and whatever else the trainer recorded (stamp, metrics) left alone. The
/// <c>baseline</c> object is optional, since policies trained before ADR 0015 do not carry one. The
/// fingerprint is taken over the file's bytes, so an edited file never passes for the one a run used.
/// </summary>
public sealed class JsonPolicySource : IPolicySource
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public PolicyFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes, DocumentOptions);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}' does not hold a policy.");
        }

        return new PolicyFile
        {
            Kind = Field<string>(root, "kind", path),
            SchemaId = Field<string>(root, "schemaId", path),
            SchemaVersion = Field<string>(root, "schemaVersion", path),
            FeatureNames = Field<string[]>(root, "featureNames", path),
            ActionKeys = Field<string[]>(root, "actionKeys", path),
            Weights = Field<double[][]>(root, "weights", path),
            Bias = Field<double[]>(root, "bias", path),
            Fallback = Number(root, "fallback", path),
            Baseline = ReadBaseline(root, path),
            Fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8],
        }.Validated();
    }

    /// <summary>The optional baseline (ADR 0015): a file written before it simply has none.</summary>
    private static PolicyBaseline? ReadBaseline(JsonElement root, string path)
    {
        if (!root.TryGetProperty("baseline", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}': the policy field 'baseline' must be an object.");
        }

        return new PolicyBaseline
        {
            Weights = Field<double[]>(element, "weights", path),
            Bias = Number(element, "bias", path),
        };
    }

    private static JsonElement Element(JsonElement root, string name, string path) =>
        root.TryGetProperty(name, out var element) && element.ValueKind != JsonValueKind.Null
            ? element
            : throw new InvalidDataException($"'{path}' lacks the policy field '{name}'.");

    private static TValue Field<TValue>(JsonElement root, string name, string path)
        where TValue : class =>
        Element(root, name, path).Deserialize<TValue>(Options)
            ?? throw new InvalidDataException($"'{path}' lacks the policy field '{name}'.");

    private static double Number(JsonElement root, string name, string path)
    {
        var element = Element(root, name, path);
        return element.ValueKind == JsonValueKind.Number
            ? element.GetDouble()
            : throw new InvalidDataException($"'{path}': the policy field '{name}' must be a number.");
    }
}
