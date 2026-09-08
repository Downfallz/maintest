using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain2.Matches.ValueObjects.Phases;

public sealed class RoundSubPhaseLifecycle
{
    // Invariant error messages
    private const string I601_NO_SUBPHASE_FLOW_FOR_PHASE =
        "I601 - No subphase flow defined for this round phase.";

    private const string I602_SUBPHASE_NOT_INITIALIZED =
        "I602 - Subphase is not initialized for this round phase.";

    private const string I603_SUBPHASE_NOT_VALID_FOR_PHASE =
        "I603 - Current subphase is not valid for this round phase.";

    private const string I604_ALREADY_AT_LAST_SUBPHASE =
        "I604 - Already at last subphase for this round phase.";

    private const string I605_SUBPHASE_NOT_ALLOWED_FOR_PHASE =
        "I605 - Target subphase is not valid for this round phase.";

    private const string I606_INVALID_SUBPHASE_TRANSITION =
        "I606 - Invalid subphase transition for this round phase.";

    private const string I607_CANNOT_MOVE_BACKWARDS_IN_FLOW =
        "I607 - Cannot move backwards in subphase flow for this round phase.";

    public RoundSubPhase? SubPhase { get; private set; } = RoundSubPhase.Start_EnergyGain;

    /// <summary>
    /// Defines the ordered subphase flow for each main round phase.
    /// </summary>
    private static readonly IReadOnlyDictionary<RoundPhase, RoundSubPhase[]> SubPhaseFlows =
        new Dictionary<RoundPhase, RoundSubPhase[]>
        {
            [RoundPhase.StartOfRound] = new[]
            {
                RoundSubPhase.Start_EnergyGain,
                RoundSubPhase.Start_OngoingEffects
            },
            [RoundPhase.Planning] = new[]
            {
                RoundSubPhase.Planning_Evolution,
                RoundSubPhase.Planning_EvolutionResolution,
                RoundSubPhase.Planning_Speed,
                RoundSubPhase.Planning_TurnOrderResolution
            },
            [RoundPhase.Combat] = new[]
            {
                RoundSubPhase.Combat_IntentSelection,
                RoundSubPhase.Combat_RevealAndTarget,
                RoundSubPhase.Combat_ActionResolution
            },
            [RoundPhase.EndOfRound] = new[]
            {
                RoundSubPhase.End_Cleanup,
                RoundSubPhase.End_Finalization
            }
        };

    /// <summary>
    /// Initializes the subphase for a given main phase.
    /// If the phase has a flow, the first subphase is selected.
    /// If no flow exists, SubPhase is set to null.
    /// </summary>
    public Result InitializeForPhase(RoundPhase phase)
    {
        if (!SubPhaseFlows.TryGetValue(phase, out var flow) || flow.Length == 0)
        {
            SubPhase = null;
            return Result.Ok();
        }

        SubPhase = flow[0];
        return Result.Ok();
    }

    /// <summary>
    /// Move to the next subphase for the given main phase, according to its flow.
    /// </summary>
    public Result MoveToNext(RoundPhase phase)
    {
        if (!SubPhaseFlows.TryGetValue(phase, out var flow) || flow.Length == 0)
            return Result.InvariantFail(I601_NO_SUBPHASE_FLOW_FOR_PHASE);

        if (SubPhase is null)
            return Result.InvariantFail(I602_SUBPHASE_NOT_INITIALIZED);

        var currentIndex = Array.IndexOf(flow, SubPhase.Value);
        if (currentIndex < 0)
            return Result.InvariantFail(I603_SUBPHASE_NOT_VALID_FOR_PHASE);

        if (currentIndex == flow.Length - 1)
            return Result.InvariantFail(I604_ALREADY_AT_LAST_SUBPHASE);

        SubPhase = flow[currentIndex + 1];
        return Result.Ok();
    }

    /// <summary>
    /// Move directly to a specific subphase for the given main phase.
    /// Only allows moving forward or staying at the same index within the flow.
    /// </summary>
    public Result MoveTo(RoundPhase phase, RoundSubPhase next)
    {
        if (!SubPhaseFlows.TryGetValue(phase, out var flow) || flow.Length == 0)
            return Result.InvariantFail(I601_NO_SUBPHASE_FLOW_FOR_PHASE);

        if (!flow.Contains(next))
            return Result.InvariantFail(I605_SUBPHASE_NOT_ALLOWED_FOR_PHASE);

        if (SubPhase is null)
        {
            SubPhase = next;
            return Result.Ok();
        }

        var currentIndex = Array.IndexOf(flow, SubPhase.Value);
        var nextIndex = Array.IndexOf(flow, next);

        if (currentIndex < 0 || nextIndex < 0)
            return Result.InvariantFail(I606_INVALID_SUBPHASE_TRANSITION);

        if (nextIndex < currentIndex)
            return Result.InvariantFail(I607_CANNOT_MOVE_BACKWARDS_IN_FLOW);

        SubPhase = next;
        return Result.Ok();
    }
}
