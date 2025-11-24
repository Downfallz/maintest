//using DA.Game.Domain2.Matches.Contexts;
//using DA.Game.Domain2.Matches.ValueObjects;
//using DA.Game.Shared.Utilities;

//namespace DA.Game.Domain2.Matches.Services.Combat;

//// ------------------------------------------------------------
//// V1 implementations (simple, Attack-only, no crit, no cost)
//// ------------------------------------------------------------

///// <summary>
///// Basic action policy V1: checks actor and target are alive and intent is not null.
///// More complex rules (phase, board state) can be added later.
///// </summary>
//public sealed class CombatActionPolicyV1 : ICombatActionPolicy
//{
//    public Result EnsureActionIsValid(PlayerActionContext ctx, CombatActionChoice intent, Match match)
//    {
//        var actor = match.FindCreature(intent.ActorId);
//        if (actor is null || actor.IsDead)
//            return Result.Fail("Invalid or dead attacker.");

//        // Here we assume a single-target attack, adjust to your shape.
//        var target = match.FindCreature(intent.TargetId);
//        if (target is null || target.IsDead)
//            return Result.Fail("Invalid or dead target.");

//        return Result.Ok();
//    }
//}
