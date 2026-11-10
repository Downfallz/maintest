using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// What a seat is being asked. The creature is the one asked about for a speed choice, an intent, a target
/// binding and an action chosen at the slot, and none for evolution or a tie order, which are asked of the
/// player rather than of one creature.
/// </summary>
public sealed record SeatQuestion(PlayerOptionsKind Kind, CreatureId? Creature)
{
    /// <summary>Whether a decision answers this question rather than another of the seat's.</summary>
    public bool Answers(PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Kind != Kind)
        {
            return false;
        }

        // Evolution names its own creature and a tie order names several; the other four are asked about one
        // creature, and the answer is for it.
        return Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.TieOrder || Creature is null || decision.Creature is null || decision.Creature == Creature;
    }
}
