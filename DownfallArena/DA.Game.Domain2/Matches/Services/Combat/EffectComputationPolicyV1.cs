//using DA.Game.Domain2.Matches.ValueObjects;
//using DA.Game.Shared.Contracts.Matches.Enums;

//namespace DA.Game.Domain2.Matches.Services.Combat;

///// <summary>
///// Effect computation V1: only handles a basic "Attack" skill with flat damage.
///// No crit, no DoT, no buffs in this first version.
///// </summary>
//public sealed class EffectComputationPolicyV1 : IEffectComputationPolicy
//{
//    private const string AttackSkillCode = "Attack";
//    private const int BaseAttackDamage = 10; // Tune or read from resources

//    public RawEffectBundle ComputeRawEffects(
//        CombatActionChoice intent,
//        IReadOnlyList<Creature> targets,
//        Match match)
//    {
//        if (intent.SkillCode != AttackSkillCode || targets.Count == 0)
//            return RawEffectBundle.Empty;

//        var effects = targets
//            .Select(t => new InstantEffectApplication(
//                t.Id,
//                EffectKind.Damage,
//                BaseAttackDamage))
//            .ToArray();

//        return new RawEffectBundle(effects, Array.Empty<ConditionApplication>());
//    }
//}
