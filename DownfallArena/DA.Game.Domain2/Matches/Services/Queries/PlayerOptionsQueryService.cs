using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.RevealAndTarget;
using DA.Game.Domain2.Matches.Services.Phases;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Queries;

public sealed class PlayerOptionsQueryService : IPlayerOptionsQueryService
{
    private readonly ISpeedProgressionEvaluatorService _speedEvaluator;
    private readonly IEvolutionProgressionEvaluatorService _evolutionEvaluator;
    private readonly ICombatPlanningProgressionEvaluatorService _combatPlanningEvaluator;
    private readonly ICombatActionProgressionEvaluatorService _combatActionEvaluator;
    private readonly ITalentQueryService _talentQueryService;
    private readonly ILegalTargetsResolverService _legalTargetsResolverService;
    public PlayerOptionsQueryService(
        ISpeedProgressionEvaluatorService speedEvaluator,
        IEvolutionProgressionEvaluatorService evolutionEvaluator,
        ICombatPlanningProgressionEvaluatorService combatPlanningEvaluator,
        ICombatActionProgressionEvaluatorService combatActionEvaluator,
        ITalentQueryService talentQueryService,
        ILegalTargetsResolverService legalTargetsResolverService)
    {
        _speedEvaluator = speedEvaluator;
        _evolutionEvaluator = evolutionEvaluator;
        _combatPlanningEvaluator = combatPlanningEvaluator;
        _combatActionEvaluator = combatActionEvaluator;
        _talentQueryService = talentQueryService;
        _legalTargetsResolverService = legalTargetsResolverService;
    }

    public Result<PlayerOptions> GetOptionsForPlayer(
        Match match,
        PlayerSlot slot,
        IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(match);

        var round = match.CurrentRound;
        if (round is null)
            return Result<PlayerOptions>.Fail("D7P0_NO_CURRENT_ROUND");

        return round.SubPhase switch
        {
            // -----------------------------
            // PLANNING — SPEED
            // -----------------------------
            RoundSubPhase.Planning_Speed =>
                BuildSpeedOptions(match, slot),

            // -----------------------------
            // PLANNING — EVOLUTION
            // -----------------------------
            RoundSubPhase.Planning_Evolution =>
                BuildEvolutionOptions(match, slot, resources),

            // -----------------------------
            // COMBAT — INTENT SELECTION
            // -----------------------------
            RoundSubPhase.Combat_IntentSelection =>
                BuildCombatPlanningOptions(match, slot),

            // -----------------------------
            // COMBAT — REVEAL & TARGET
            // -----------------------------
            RoundSubPhase.Combat_RevealAndTarget =>
                BuildCombatActionOptions(match),

            // -----------------------------
            // Everything else → no external input required
            // -----------------------------
            _ =>
                Result<PlayerOptions>.Ok(new PlayerOptions
                {
                    Phase = round.Phase,
                    SubPhase = round.SubPhase!.Value
                })
        };
    }

    // =================================================
    // Planning_Speed
    // =================================================
    private Result<PlayerOptions> BuildSpeedOptions(
        Match match,
        PlayerSlot slot)
    {
        var gate = _speedEvaluator.Evaluate(match);
        if (!gate.IsSuccess)
            return Result<PlayerOptions>.Fail(gate.Error!);

        var missing = slot == PlayerSlot.Player1
            ? gate.Value!.Player1MissingCreatureIds
            : gate.Value!.Player2MissingCreatureIds;

        return Result<PlayerOptions>.Ok(new PlayerOptions
        {
            Phase = match.CurrentRound!.Phase,
            SubPhase = match.CurrentRound!.SubPhase!.Value,

            Speed = new SpeedOptions
            {
                Remaining = missing.Count,
                RequiredCreatures = missing
            }
        });
    }

    // =================================================
    // Planning_Evolution
    // =================================================
    private Result<PlayerOptions> BuildEvolutionOptions(
    Match match,
    PlayerSlot slot,
    IGameResources resources)
    {
        var gate = _evolutionEvaluator.Evaluate(match, resources);
        if (!gate.IsSuccess)
            return Result<PlayerOptions>.Fail(gate.Error!);

        var remaining = slot == PlayerSlot.Player1
            ? gate.Value!.Player1RemainingPicks
            : gate.Value!.Player2RemainingPicks;

        IReadOnlyList<CreatureId> legalCreatures = Array.Empty<CreatureId>();

        if (remaining > 0)
        {
            // Ask domain for unlockable spells (single source of truth)
            var unlockablesRes =
                _talentQueryService.GetUnlockableSpellsForPlayer(match, slot, resources);

            if (!unlockablesRes.IsSuccess)
                return Result<PlayerOptions>.Fail(unlockablesRes.Error!);

            // Legal creature = alive + has at least one unlockable spell
            legalCreatures = unlockablesRes.Value!.Creatures
                .Where(c => c.UnlockableSpellIds.Count > 0)
                .Select(c => c.CreatureId)
                .ToArray();
        }

        return Result<PlayerOptions>.Ok(new PlayerOptions
        {
            Phase = match.CurrentRound!.Phase,
            SubPhase = match.CurrentRound!.SubPhase!.Value,

            Evolution = new EvolutionOptions
            {
                RemainingPicks = remaining,
                LegalCreatureIds = legalCreatures
            }
        });
    }

    // =================================================
    // Combat_IntentSelection
    // =================================================
    private Result<PlayerOptions> BuildCombatPlanningOptions(
        Match match,
        PlayerSlot slot)
    {
        var gate = _combatPlanningEvaluator.Evaluate(match);
        if (!gate.IsSuccess)
            return Result<PlayerOptions>.Fail(gate.Error!);

        var missingForPlayer = gate.Value!.MissingCreatureIds
            .Where(id =>
                match.AllCreatures.Any(c =>
                    c.Id == id && c.OwnerSlot == slot))
            .ToArray();

        return Result<PlayerOptions>.Ok(new PlayerOptions
        {
            Phase = match.CurrentRound!.Phase,
            SubPhase = match.CurrentRound!.SubPhase!.Value,

            CombatPlanning = new CombatPlanningOptions
            {
                MissingCreatureIds = missingForPlayer
            }
        });
    }

    // =================================================
    // Combat_RevealAndTarget
    // =================================================
    private Result<PlayerOptions> BuildCombatActionOptions(Match match)
    {
        var gate = _combatActionEvaluator.Evaluate(match);
        if (!gate.IsSuccess)
            return Result<PlayerOptions>.Fail(gate.Error!);

        var round = match.CurrentRound!;
        var remaining = gate.Value!.RemainingReveals;
        var nextActorId = gate.Value!.NextActorId;

        if (nextActorId is null)
        {
            return Result<PlayerOptions>.Ok(new PlayerOptions
            {
                Phase = round.Phase,
                SubPhase = round.SubPhase!.Value,
                CombatAction = new CombatActionOptions
                {
                    RemainingReveals = remaining,
                    NextActorId = null,
                    MinTargets = 0,
                    MaxTargets = 0,
                    LegalTargetIds = Array.Empty<CreatureId>()
                }
            });
        }

        // Snapshot once
        var snapshots = match.AllCreatures
            .Select(CreatureSnapshot.From)
            .ToArray();

        var actor = snapshots.SingleOrDefault(c => c.CharacterId == nextActorId);
        if (actor is null)
            return Result<PlayerOptions>.Fail("D7A3_NEXT_ACTOR_NOT_FOUND");

        // Find intent for this actor
        var intent = TryGetIntent(round, nextActorId.Value);
        if (intent is null)
            return Result<PlayerOptions>.Fail("D7A4_INTENT_MISSING_FOR_NEXT_ACTOR");

        var ctxResult = CreaturePerspective.FromMatch(actor.CharacterId, match);
        if (!ctxResult.IsSuccess)
            return Result<PlayerOptions>.Fail(ctxResult.Error!);

        var legalRes = _legalTargetsResolverService.Resolve(ctxResult.Value!, intent.SpellRef);
        if (!legalRes.IsSuccess)
            return Result<PlayerOptions>.Fail(legalRes.Error!);

        return Result<PlayerOptions>.Ok(new PlayerOptions
        {
            Phase = round.Phase,
            SubPhase = round.SubPhase!.Value,
            CombatAction = new CombatActionOptions
            {
                RemainingReveals = remaining,
                NextActorId = nextActorId,
                MinTargets = legalRes.Value!.MinTargets,
                MaxTargets = legalRes.Value!.MaxTargets,
                LegalTargetIds = legalRes.Value!.LegalTargetIds
            }
        });
    }

    private static CombatActionIntent? TryGetIntent(Round round, CreatureId actorId)
    {
        if (round.Player1CombatIntentsByCreature is not null
            && round.Player1CombatIntentsByCreature.TryGetValue(actorId, out var i1))
            return i1;

        if (round.Player2CombatIntentsByCreature is not null
            && round.Player2CombatIntentsByCreature.TryGetValue(actorId, out var i2))
            return i2;

        return null;
    }
}
