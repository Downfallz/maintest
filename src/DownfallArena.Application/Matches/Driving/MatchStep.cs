using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Where a stepped match stopped: over, or waiting for the people it names.
/// </summary>
/// <param name="Outcome">How the match ended, or none while it is being played.</param>
/// <param name="Held">The answers given and not yet handed to the match, to pass to the next step.</param>
/// <param name="Asked">The seats a person has a question to answer at, in seat order; none once the match is over.</param>
/// <param name="Decided">
/// Every decision this step took, the one submitted first and then each Player agent's, in the order that
/// submitting them again to two seats of people rebuilds the match.
/// </param>
public sealed record MatchStep(MatchOutcome? Outcome, HeldAnswers Held, IReadOnlyList<PlayerSlot> Asked, IReadOnlyList<SeatDecision> Decided)
{
    public bool IsOver => Outcome is not null;

    /// <summary>
    /// Whether the Player agents still owe decisions: the match is not over and no person is asked anything.
    /// Only a step told to stop after one of the agents' turns ends this way; it is taken again for the rest.
    /// </summary>
    public bool Owed => Outcome is null && Asked.Count == 0;
}
