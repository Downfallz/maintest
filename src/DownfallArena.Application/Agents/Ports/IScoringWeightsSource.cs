namespace DownfallArena.Application.Agents.Ports;

/// <summary>
/// Where the heuristic agent's weights come from: a file named by the agent spec's path. Owned by Application,
/// implemented by Infrastructure.
/// </summary>
public interface IScoringWeightsSource
{
    ScoringWeights Load(string path);
}
