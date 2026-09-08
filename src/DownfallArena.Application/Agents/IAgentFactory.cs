using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The agent registry: turns a spec into a seated agent, with the random source its decisions draw from.
/// </summary>
public interface IAgentFactory
{
    IPlayerAgent Create(AgentSpec spec, IRandomSource random);
}
