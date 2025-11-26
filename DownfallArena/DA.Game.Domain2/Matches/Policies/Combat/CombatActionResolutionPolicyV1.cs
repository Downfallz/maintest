using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;
using System;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class CombatActionResolutionPolicyV1 : ICombatActionPolicy
{
    public Result EnsureActionIsValid(
        GameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.State != MatchState.Started)
            return Result.Fail("Match is not started.");
        if (ctx.Phase != RoundPhase.CombatResolution)
            return Result.Fail("Roound phase is not combat");

        if (ctx.Actor.IsDead)
            return Result.Fail("Dead creature can't play spell");
        
        if (ctx.Actor.IsStunned)
            return Result.Fail("Stunned creature can't play spell");

        if (ctx.Timeline is null)
            return Result.Fail("Timeline non initialisé pour ce round.");

        if (ctx.Cursor!.IsEnd)
            return Result.Fail("Le round est déjà complété.");

        var slot = ctx.Timeline.Slots[ctx.Cursor.Index-1];
        if (ctx.ActorId != slot.CombatCharacter.Id)
            return Result.Fail("Not that creature spell to resolve.");

        if (!ctx.CombatActionChoices!.TryGetValue(slot.CombatCharacter.Id, out var intent))
            return Result.Fail("Rien de soumis pour ce character.");

        return Result.Ok();
    }
}
