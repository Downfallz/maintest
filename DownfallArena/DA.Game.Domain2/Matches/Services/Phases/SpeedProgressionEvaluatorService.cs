using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
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
/// Evaluates whether the current round can progress past the Speed selection gate.
/// </summary>
public sealed class SpeedProgressionEvaluatorService : ISpeedProgressionEvaluatorService
{
    public Result<SpeedGateResult> Evaluate(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var round = match.CurrentRound;
        if (round is null)
            return Result<SpeedGateResult>.Fail("D7S0_NO_CURRENT_ROUND");

        // Snapshot once (avoid touching entities multiple times)
        var allSnapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToArray();

        var p1Missing = ComputeMissingForSlot(PlayerSlot.Player1, allSnapshots, round.Player1SpeedChoices);
        var p2Missing = ComputeMissingForSlot(PlayerSlot.Player2, allSnapshots, round.Player2SpeedChoices);

        var result = new SpeedGateResult(
            CanAdvance: p1Missing.Count == 0 && p2Missing.Count == 0,
            Player1MissingCreatureIds: p1Missing,
            Player2MissingCreatureIds: p2Missing
        );

        return Result<SpeedGateResult>.Ok(result);
    }

    private static IReadOnlyList<CreatureId> ComputeMissingForSlot(
        PlayerSlot slot,
        IReadOnlyList<CreatureSnapshot> allSnapshots,
        IReadOnlyCollection<SpeedChoice> submittedChoices)
    {
        // Build a set of creature ids that already submitted a speed choice
        var submitted = (submittedChoices ?? Array.Empty<SpeedChoice>())
            .Select(x => x.CreatureId) // assumes SpeedChoice has CreatureId
            .ToHashSet();

        // "Missing" = alive + owned by slot + not stunned + no submitted speed yet
        var missing = allSnapshots
            .Where(c => c.OwnerSlot == slot)
            .Where(c => c.IsAlive)
            .Where(c => !c.IsStunned) // per your rule: stunned can't choose
            .Where(c => !submitted.Contains(c.CharacterId))
            .Select(c => c.CharacterId)
            .ToArray();

        return missing;
    }
}