//using DA.Game.Domain2.Matches.ValueObjects;

//namespace DA.Game.Domain2.Matches.Services.Combat;

///// <summary>
///// Targeting policy V1: assumes the action already carries a single TargetId.
///// </summary>
//public sealed class TargetingPolicyV1 : ITargetingPolicy
//{
//    public IReadOnlyList<Creature> ResolveTargets(CombatActionChoice intent, Match match)
//    {
//        var target = match.FindCreature(intent.TargetId);
//        if (target is null)
//            return Array.Empty<Creature>();

//        return new[] { target };
//    }
//}
