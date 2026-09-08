using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CombatActionSelectionPolicyV1() : ICombatActionSelectionPolicy
{
    // -------------------------------
    // INVARIANT FAILURES (Ixxx)
    // -------------------------------
    private const string INV_I101_MATCH_NOT_STARTED =
        "I101 - Match must be started to submit a combat action.";

    private const string INV_I102_PHASE_NOT_COMBAT =
        "I102 - Round phase must be Combat to submit a combat action.";

    // -------------------------------
    // DOMAIN FAILURES (Dxxx)
    // -------------------------------
    private const string DOM_D201_ACTOR_DEAD =
        "D201 - Dead creature cannot submit a combat action.";

    private const string DOM_D202_ACTOR_STUNNED =
        "D202 - Stunned creature cannot submit a combat action.";

    private const string DOM_D203_ALREADY_SUBMITTED =
        "D203 - This creature has already submitted a combat action for this round.";

    public Result EnsureActionIsValid(CreaturePerspective ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // Invariant failures (orchestration bugs)
        if (ctx.State != MatchState.Started)
            return Result.InvariantFail(INV_I101_MATCH_NOT_STARTED);

        if (ctx.Phase is not RoundPhase.Combat)
            return Result.InvariantFail(INV_I102_PHASE_NOT_COMBAT);

        // Domain / game-flow failures
        if (ctx.Actor.IsDead)
            return Result.Fail(DOM_D201_ACTOR_DEAD);

        if (ctx.Actor.IsStunned)
            return Result.Fail(DOM_D202_ACTOR_STUNNED);

        if (ctx.CombatActionChoices?.ContainsKey(ctx.ActorId) == true)
            return Result.Fail(DOM_D203_ALREADY_SUBMITTED);

        return Result.Ok();
    }
}
