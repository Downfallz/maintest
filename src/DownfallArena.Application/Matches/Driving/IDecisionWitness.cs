using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Sees every decision a stepped match takes, as the agent that takes it is asked. Whoever decides for a seat --
/// a Player agent, or a decision submitted from outside -- is asked through what this returns, with the board
/// and the options the driver would have handed an agent there. It is how a match is recorded with nothing
/// seated to record through, and it sees a match being rebuilt from its journal the same way (ADR 0159).
/// </summary>
public interface IDecisionWitness
{
    /// <summary>The decider to ask instead: one that asks <paramref name="decider" /> and notes what it is asked and answers.</summary>
    IPlayerAgent Around(MatchId matchId, PlayerSlot slot, IPlayerAgent decider);
}
