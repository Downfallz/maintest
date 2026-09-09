namespace DownfallArena.Application.Agents.Ports;

/// <summary>
/// Where a policy agent's file comes from: a <c>policy.json</c> named by the agent spec's path, written by the
/// Python side (docs/learning/training.md). Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IPolicySource
{
    PolicyFile Load(string path);
}
