using DownfallArena.Application.Agents;

namespace DownfallArena.Cli.Studio;

/// <summary>The run panel's form: a mode, two agent specs, how many seeds, and the seed to start from.</summary>
internal sealed record StudioRunRequest
{
    public string Mode { get; init; } = StudioRunModes.Match;

    public string Player1 { get; init; } = "random";

    public string Player2 { get; init; } = "random";

    public int Matches { get; init; } = 20;

    public int? Seed { get; init; }

    public AgentSpec Agent1 => AgentSpec.Parse(Player1);

    public AgentSpec Agent2 => AgentSpec.Parse(Player2);
}
