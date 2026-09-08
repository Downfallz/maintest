using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Phases;


/// <summary>
/// Evaluates whether the Reveal step is complete and the round can move to Resolve.
/// This evaluator does NOT validate targets; that must be enforced by Ensure... policies
/// when a player submits/reveals the action.
/// </summary>
public sealed class CombatActionProgressionEvaluatorService
    : ICombatActionProgressionEvaluatorService
{
    public Result<CombatActionGateResult> Evaluate(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var round = match.CurrentRound;
        if (round is null)
            return Result<CombatActionGateResult>.Fail("D7A0_NO_CURRENT_ROUND");

        var timeline = round.Timeline;
        if (timeline is null)
            return Result<CombatActionGateResult>.Fail("D7A1_NO_TIMELINE");

        var cursor = round.RevealCursor;
        if (cursor is null)
            return Result<CombatActionGateResult>.Fail("D7A2_NO_REVEAL_CURSOR");

        var totalSlots = timeline.Count;

        // No slots => nothing to reveal => can advance.
        if (totalSlots == 0)
        {
            return Result<CombatActionGateResult>.Ok(new CombatActionGateResult(
                CanAdvance: true,
                RemainingReveals: 0,
                NextActorId: null));
        }

        var isComplete = cursor.IsEnd(totalSlots);
        if (isComplete)
        {
            return Result<CombatActionGateResult>.Ok(new CombatActionGateResult(
                CanAdvance: true,
                RemainingReveals: 0,
                NextActorId: null));
        }

        // Next reveal is the actor at the current slot index.
        var nextSlot = timeline[cursor.Index];
        var remaining = totalSlots - cursor.Index;

        return Result<CombatActionGateResult>.Ok(new CombatActionGateResult(
            CanAdvance: false,
            RemainingReveals: remaining,
            NextActorId: nextSlot.CreatureId
        ));
    }
}
