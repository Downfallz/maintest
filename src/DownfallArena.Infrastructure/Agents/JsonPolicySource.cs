using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;

namespace DownfallArena.Infrastructure.Agents;

/// <summary>
/// Reads a <c>policy.json</c> written by the Python side (docs/learning/training.md): plain values, camelCase,
/// the fields the engine needs and whatever else the trainer recorded (stamp, metrics) left alone. The
/// fingerprint is taken over the file's bytes, so an edited file never passes for the one a run used.
/// </summary>
public sealed class JsonPolicySource : IPolicySource
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public PolicyFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        var document = JsonSerializer.Deserialize<PolicyDocument>(bytes, Options)
            ?? throw new InvalidDataException($"'{path}' does not hold a policy.");
        return new PolicyFile
        {
            Kind = Required(document.Kind, "kind", path),
            SchemaId = Required(document.SchemaId, "schemaId", path),
            SchemaVersion = Required(document.SchemaVersion, "schemaVersion", path),
            FeatureNames = Required(document.FeatureNames, "featureNames", path),
            ActionKeys = Required(document.ActionKeys, "actionKeys", path),
            Weights = Required(document.Weights, "weights", path),
            Bias = Required(document.Bias, "bias", path),
            Fallback = document.Fallback ?? throw new InvalidDataException($"'{path}' lacks the policy field 'fallback'."),
            Fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8],
        }.Validated();
    }

    private static TValue Required<TValue>(TValue? value, string name, string path) =>
        value ?? throw new InvalidDataException($"'{path}' lacks the policy field '{name}'.");

    private sealed class PolicyDocument
    {
        public string? Kind { get; init; }

        public string? SchemaId { get; init; }

        public string? SchemaVersion { get; init; }

        public string[]? FeatureNames { get; init; }

        public string[]? ActionKeys { get; init; }

        public double[][]? Weights { get; init; }

        public double[]? Bias { get; init; }

        public double? Fallback { get; init; }
    }
}
