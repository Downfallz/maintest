using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Phases;
/// <summary>
/// Evaluates whether the current round can progress past the Combat Planning gate.
/// A combat intent is required for each alive, non-stunned creature.
/// </summary>
public sealed class CombatPlanningProgressionEvaluatorService
    : ICombatPlanningProgressionEvaluatorService
{
    public Result<CombatPlanningGateResult> Evaluate(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var round = match.CurrentRound;
        if (round is null)
            return Result<CombatPlanningGateResult>.Fail("D7C0_NO_CURRENT_ROUND");

        // Snapshot creatures once
        var allSnapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToArray();

        var missing = new List<CreatureId>();

        // Player 1
        missing.AddRange(
            ComputeMissingForSlot(
                PlayerSlot.Player1,
                allSnapshots,
                round.Player1CombatIntentsByCreature));

        // Player 2
        missing.AddRange(
            ComputeMissingForSlot(
                PlayerSlot.Player2,
                allSnapshots,
                round.Player2CombatIntentsByCreature));

        var result = new CombatPlanningGateResult(
            CanAdvance: missing.Count == 0,
            MissingCreatureIds: missing
        );

        return Result<CombatPlanningGateResult>.Ok(result);
    }

    private static IReadOnlyList<CreatureId> ComputeMissingForSlot(
        PlayerSlot slot,
        IReadOnlyList<CreatureSnapshot> allSnapshots,
        IReadOnlyDictionary<CreatureId, CombatActionIntent>? submittedIntents)
    {
        var submitted = submittedIntents?.Keys.ToHashSet()
            ?? new HashSet<CreatureId>();

        var missing = allSnapshots
            .Where(c => c.OwnerSlot == slot)
            .Where(c => c.IsAlive)
            .Where(c => !c.IsStunned)
            .Where(c => !submitted.Contains(c.CharacterId))
            .Select(c => c.CharacterId)
            .ToArray();

        return missing;
    }
}