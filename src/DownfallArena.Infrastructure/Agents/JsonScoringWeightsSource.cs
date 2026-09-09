using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;

namespace DownfallArena.Infrastructure.Agents;

/// <summary>
/// Reads a heuristic agent's weights from a JSON file such as <c>learning/weights/greedy.json</c>: one number
/// per weight, camelCase, a missing weight keeps the built-in value, an unknown one is an error.
/// </summary>
public sealed class JsonScoringWeightsSource : IScoringWeightsSource
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ScoringWeights Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = JsonSerializer.Deserialize<WeightsFile>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"'{path}' does not hold scoring weights.");
        var defaults = ScoringWeights.Default;
        return new ScoringWeights(
            file.Damage ?? defaults.Damage,
            file.Kill ?? defaults.Kill,
            file.Heal ?? defaults.Heal,
            file.Stun ?? defaults.Stun,
            file.Bleed ?? defaults.Bleed,
            file.Buff ?? defaults.Buff,
            file.Energy ?? defaults.Energy,
            file.Risk ?? defaults.Risk,
            file.Initiative ?? defaults.Initiative).Validated();
    }

    private sealed record WeightsFile(double? Damage, double? Kill, double? Heal, double? Stun, double? Bleed, double? Buff, double? Energy, double? Risk, double? Initiative);
}
