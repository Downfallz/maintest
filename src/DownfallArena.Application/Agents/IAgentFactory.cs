using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The agent registry: turns a spec into a seated agent for a rule set, with the random source its decisions
/// may draw from.
/// </summary>
public interface IAgentFactory
{
    IPlayerAgent Create(AgentSpec spec, RuleSet rules, IRandomSource random);
}
