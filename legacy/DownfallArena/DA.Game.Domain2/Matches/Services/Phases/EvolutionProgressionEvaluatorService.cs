using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Queries;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Phases;
/// <summary>
/// Evaluates whether the current round can progress past the Evolution (Spell Unlock) gate.
/// </summary>
public sealed class EvolutionProgressionEvaluatorService : IEvolutionProgressionEvaluatorService
{
    private const int MaxEvolutionPicksPerRound = 2;

    private readonly ITalentQueryService _talentQueryService;

    public EvolutionProgressionEvaluatorService(
        ITalentQueryService talentQueryService)
    {
        _talentQueryService = talentQueryService ?? throw new ArgumentNullException(nameof(talentQueryService));
    }

    public Result<EvolutionGateResult> Evaluate(Match match, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(match);

        var round = match.CurrentRound;
        if (round is null)
            return Result<EvolutionGateResult>.Fail("D7E0_NO_CURRENT_ROUND");

        // Snapshot once
        var allSnapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToArray();

        var p1AliveIds = allSnapshots
            .Where(c => c.OwnerSlot == PlayerSlot.Player1)
            .Where(c => c.IsAlive)
            .Select(c => c.CharacterId)
            .ToHashSet();

        var p2AliveIds = allSnapshots
            .Where(c => c.OwnerSlot == PlayerSlot.Player2)
            .Where(c => c.IsAlive)
            .Select(c => c.CharacterId)
            .ToHashSet();

        var p1Submitted = round.Player1EvolutionChoices?.Count ?? 0;
        var p2Submitted = round.Player2EvolutionChoices?.Count ?? 0;

        var p1RemainingRaw = Math.Max(0, MaxEvolutionPicksPerRound - p1Submitted);
        var p2RemainingRaw = Math.Max(0, MaxEvolutionPicksPerRound - p2Submitted);

        // If both already submitted max picks, we can advance immediately.
        if (p1RemainingRaw == 0 && p2RemainingRaw == 0)
        {
            var done = new EvolutionGateResult(
                CanAdvance: true,
                Player1RemainingPicks: 0,
                Player2RemainingPicks: 0);

            return Result<EvolutionGateResult>.Ok(done);
        }

        // Compute how many unlocks are actually possible right now (can be 0).
        var p1UnlockablesRes = _talentQueryService.GetUnlockableSpellsForPlayer(match, PlayerSlot.Player1, resources);
        if (!p1UnlockablesRes.IsSuccess)
            return Result<EvolutionGateResult>.Fail(p1UnlockablesRes.Error!);

        var p2UnlockablesRes = _talentQueryService.GetUnlockableSpellsForPlayer(match, PlayerSlot.Player2, resources);
        if (!p2UnlockablesRes.IsSuccess)
            return Result<EvolutionGateResult>.Fail(p2UnlockablesRes.Error!);

        var p1TotalUnlockables = CountUnlockablesForAliveCreatures(p1UnlockablesRes.Value!, p1AliveIds);
        var p2TotalUnlockables = CountUnlockablesForAliveCreatures(p2UnlockablesRes.Value!, p2AliveIds);

        // Effective remaining picks cannot exceed how many unlockable spells exist.
        var p1RemainingEffective = Math.Min(p1RemainingRaw, p1TotalUnlockables);
        var p2RemainingEffective = Math.Min(p2RemainingRaw, p2TotalUnlockables);

        var result = new EvolutionGateResult(
            CanAdvance: p1RemainingEffective == 0 && p2RemainingEffective == 0,
            Player1RemainingPicks: p1RemainingEffective,
            Player2RemainingPicks: p2RemainingEffective);

        return Result<EvolutionGateResult>.Ok(result);
    }

    private static int CountUnlockablesForAliveCreatures(
        PlayerUnlockableSpells unlockables,
        ISet<CreatureId> aliveIds)
    {
        // NOTE: Adjust property names if your model differs.
        // Expected shape: unlockables.Creatures -> each has CreatureId + UnlockableSpellIds
        var total = 0;

        foreach (var c in unlockables.Creatures)
        {
            if (!aliveIds.Contains(c.CreatureId))
                continue;

            total += c.UnlockableSpellIds?.Count ?? 0;
        }

        return total;
    }
}

