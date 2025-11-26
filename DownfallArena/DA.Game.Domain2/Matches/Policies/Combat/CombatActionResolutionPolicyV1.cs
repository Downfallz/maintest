using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CombatActionResolutionPolicyV1 : ICombatActionPolicy
{
    private const string ErrorMatchNotStarted =
        "CR1 - Match must be started to resolve a combat action.";

    private const string ErrorPhaseNotCombat =
        "CR2 - Round phase must be Combat to resolve a combat action.";

    private const string ErrorActorDead =
        "CR3 - Dead creature cannot resolve a combat action.";

    private const string ErrorActorStunned =
        "CR4 - Stunned creature cannot resolve a combat action.";

    private const string ErrorTimelineMissing =
        "CR5 - Timeline is not initialized for this round.";

    private const string ErrorCursorMissing =
        "CR6 - Round cursor is not initialized.";

    private const string ErrorRoundAlreadyCompleted =
        "CR7 - Round is already completed; no further combat actions can be resolved.";

    private const string ErrorCursorOutOfRange =
        "CR8 - Timeline cursor is out of range for this round.";

    private const string ErrorNotActorsTurn =
        "CR9 - It is not this creature's turn to resolve a combat action.";

    private const string ErrorNoSubmittedAction =
        "CR10 - No combat action was submitted for this creature.";

    public Result EnsureActionIsValid(CreaturePerspective ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // Match & phase
        if (ctx.State != MatchState.Started)
            return Result.Fail(ErrorMatchNotStarted);

        if (ctx.Phase is not RoundPhase.Combat)
            return Result.Fail(ErrorPhaseNotCombat);

        // Actor state
        if (ctx.Actor.IsDead)
            return Result.Fail(ErrorActorDead);

        if (ctx.Actor.IsStunned)
            return Result.Fail(ErrorActorStunned);

        // Timeline & cursor
        if (ctx.Timeline is null)
            return Result.Fail(ErrorTimelineMissing);

        if (ctx.Cursor is null)
            return Result.Fail(ErrorCursorMissing);

        if (ctx.Cursor.IsEnd)
            return Result.Fail(ErrorRoundAlreadyCompleted);

        var index = ctx.Cursor.Index;

        if (index <= 0 || index > ctx.Timeline.Slots.Count)
            return Result.Fail(ErrorCursorOutOfRange);

        var slot = ctx.Timeline.Slots[index - 1];

        // Turn ownership
        if (ctx.ActorId != slot.CombatCharacter.Id)
            return Result.Fail(ErrorNotActorsTurn);

        // Submitted intent
        if (ctx.CombatActionChoices is null ||
            !ctx.CombatActionChoices.TryGetValue(slot.CombatCharacter.Id, out _))
        {
            return Result.Fail(ErrorNoSubmittedAction);
        }

        return Result.Ok();
    }
}
