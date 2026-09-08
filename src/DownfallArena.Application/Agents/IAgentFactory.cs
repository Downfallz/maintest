using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The agent registry: turns a spec into a seated agent for a rule set, with the random source its decisions
/// may draw from, and resolves a spec into the identity a run stamp should carry.
/// </summary>
public interface IAgentFactory
{
    /// <summary>The spec with its version filled in: for a heuristic agent, a fingerprint of the weights the file holds now.</summary>
    AgentSpec Resolve(AgentSpec spec);

    IPlayerAgent Create(AgentSpec spec, RuleSet rules, IRandomSource random);
}
