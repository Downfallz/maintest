using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class AttackChoicePolicyV1 : IAttackChoicePolicy
{
    public Result EnsureActionIsValid(
        GameContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.State != MatchState.Started)
            return Result.Fail("Match is not started.");
        if (ctx.Phase != RoundPhase.AttackChoice)
            return Result.Fail("Roound phase is not combat");

        if (ctx.Actor.IsDead)
            return Result.Fail("Dead creature can't play spell");
        
        if (ctx.Actor.IsStunned)
            return Result.Fail("Stunned creature can't play spell");
        if (ctx.CombatActionChoices!.ContainsKey(ctx.ActorId))
            return Result.Fail("Cette créature a déjà soumis une action pour ce round.");
        return Result.Ok();
    }
}
