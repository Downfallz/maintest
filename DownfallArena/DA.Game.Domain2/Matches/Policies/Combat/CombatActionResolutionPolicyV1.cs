using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CombatActionResolutionPolicyV1 : ICombatActionResolutionPolicy
{
    // -------------------------------
    // INVARIANT FAILURES (Ixxx)
    // -------------------------------
    private const string INV_I001_MATCH_NOT_STARTED =
        "I001 - Match must be started to resolve a combat action.";

    private const string INV_I002_PHASE_NOT_COMBAT =
        "I002 - Round phase must be Combat to resolve a combat action.";

    private const string INV_I003_TIMELINE_MISSING =
        "I003 - Timeline is not initialized for this round.";

    private const string INV_I004_ROUND_ALREADY_COMPLETED =
        "I004 - Round is already completed; no further combat actions can be resolved.";

    private const string INV_I005_CURRENT_SLOT_MISSING =
        "I005 - No current activation slot available for this round.";

    private const string INV_I006_NOT_ACTORS_TURN =
        "I006 - It is not this creature's turn to resolve a combat action.";

    // -------------------------------
    // DOMAIN FAILURES (Dxxx)
    // -------------------------------
    private const string DOM_D101_ACTOR_DEAD =
        "D101 - Dead creature cannot resolve a combat action.";

    private const string DOM_D102_ACTOR_STUNNED =
        "D102 - Stunned creature cannot resolve a combat action.";

    private const string DOM_D103_NO_SUBMITTED_ACTION =
        "D103 - No combat action was submitted for this creature.";

    public Result EnsureActionIsValid(CreaturePerspective ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // Invariant failures
        if (ctx.State != MatchState.Started)
            return Result.InvariantFail(INV_I001_MATCH_NOT_STARTED);

        if (ctx.Phase is not RoundPhase.Combat)
            return Result.InvariantFail(INV_I002_PHASE_NOT_COMBAT);

        if (ctx.Timeline is null)
            return Result.InvariantFail(INV_I003_TIMELINE_MISSING);

        if (ctx.Timeline.IsComplete)
            return Result.InvariantFail(INV_I004_ROUND_ALREADY_COMPLETED);

        var slot = ctx.Timeline.Current;
        if (slot is null)
            return Result.InvariantFail(INV_I005_CURRENT_SLOT_MISSING);

        if (ctx.ActorId != slot.CreatureId)
            return Result.InvariantFail(INV_I006_NOT_ACTORS_TURN);

        // Domain failures
        if (ctx.Actor.IsDead)
            return Result.Fail(DOM_D101_ACTOR_DEAD);

        if (ctx.Actor.IsStunned)
            return Result.Fail(DOM_D102_ACTOR_STUNNED);

        if (ctx.CombatActionChoices is null ||
            !ctx.CombatActionChoices.TryGetValue(slot.CreatureId, out _))
        {
            return Result.Fail(DOM_D103_NO_SUBMITTED_ACTION);
        }

        return Result.Ok();
    }
}
