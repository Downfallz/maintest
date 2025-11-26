using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CombatActionSelectionPolicyV1() : IAttackChoicePolicy
{
    private const string ErrorMatchNotStarted =
        "R1 - Match must be started to submit a combat action.";

    private const string ErrorPhaseNotCombat =
        "R2 - Round phase must be Combat to submit a combat action.";

    private const string ErrorActorDead =
        "R3 - Dead creature cannot submit a combat action.";

    private const string ErrorActorStunned =
        "R4 - Stunned creature cannot submit a combat action.";

    private const string ErrorAlreadySubmitted =
        "R5 - This creature has already submitted a combat action for this round.";

    public Result EnsureActionIsValid(CreaturePerspective ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.State != MatchState.Started)
            return Result.Fail(ErrorMatchNotStarted);

        if (ctx.Phase != RoundPhase.Combat)
            return Result.Fail(ErrorPhaseNotCombat);

        if (ctx.Actor.IsDead)
            return Result.Fail(ErrorActorDead);

        if (ctx.Actor.IsStunned)
            return Result.Fail(ErrorActorStunned);

        if (ctx.CombatActionChoices?.ContainsKey(ctx.ActorId) == true)
            return Result.Fail(ErrorAlreadySubmitted);

        return Result.Ok();
    }
}
