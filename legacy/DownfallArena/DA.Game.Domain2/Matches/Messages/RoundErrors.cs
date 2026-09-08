using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Messages;

/// <summary>
/// Centralized round error codes.
/// Dxxx = domain / game-flow errors.
/// Ixxx = invariant / impossible-state errors.
/// </summary>
/// <summary>
/// Centralized round error codes.
/// Dxxx = domain / game-flow errors (user / rules).
/// Ixxx = invariant / impossible-state errors (engine bug).
/// </summary>
public static class RoundErrorCodes
{
    // ---------------
    // Invariants (I)
    // ---------------

    public const string I001_INVALID_PHASE_TRANSITION =
        "I001 - Invalid round phase transition.";

    public const string I301_INVALID_PHASE_FOR_REVEAL =
        "I301: Invalid phase/subphase for revealing next combat intent.";

    public const string I302_INVALID_PHASE_FOR_RESOLUTION =
        "I302: Invalid phase/subphase for resolving next combat action.";

    public const string I401_TIMELINE_NOT_INITIALIZED =
        "I401 - Timeline not initialized for this round.";

    public const string I402_ALL_INTENTS_ALREADY_REVEALED =
        "I402 - All combat intents are already revealed for this round.";

    public const string I403_NO_INTENT_FOR_CREATURE =
        "I403 - No combat intent found for the current creature in this round.";

    public const string I404_COMBAT_RESOLUTION_ALREADY_COMPLETED =
        "I404 - Combat resolution is already completed for this round.";

    public const string I405_NO_ACTION_FOR_CREATURE =
        "I405 - No combat action found for the current creature in this round.";

    public const string I501_INVALID_PHASE_FOR_ROUND_FINALIZATION =
        "I501 - Invalid phase or subphase for round finalization.";

    // ---------------
    // Domain (D)
    // ---------------

    // Evolution
    public const string D101_INVALID_PHASE_EVOLUTION =
        "D101 - Invalid phase for submitting an evolution choice.";

    public const string D102_EVOLUTION_ALREADY_SUBMITTED =
        "D102 - Evolution choice already submitted for this creature.";

    // Speed
    public const string D201_INVALID_PHASE_SPEED =
        "D201 - Invalid phase for submitting a speed choice.";

    public const string D202_SPEED_ALREADY_SUBMITTED =
        "D202 - Speed choice already submitted for this creature.";

    // Combat intents
    public const string D301_INVALID_PHASE_INTENT =
        "D301 - Invalid phase for submitting a combat intent.";

    public const string D302_INTENT_ALREADY_SUBMITTED =
        "D302 - Combat intent already submitted for this creature.";

    // Combat actions
    public const string D351_INVALID_PHASE_COMBAT_ACTION =
        "D351 - Invalid phase for submitting a combat action.";

    public const string D352_COMBAT_ACTION_ALREADY_SUBMITTED =
        "D352 - Combat action already submitted for this creature.";
}