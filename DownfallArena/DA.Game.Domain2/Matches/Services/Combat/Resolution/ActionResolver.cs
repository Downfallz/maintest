//using DA.Game.Domain2.Matches.RuleSets;
//using DA.Game.Domain2.Matches.ValueObjects;
//using DA.Game.Shared.Utilities;

//namespace DA.Game.Domain2.Matches.Services;

//public sealed class ActionResolver
//{
//    private readonly ICombatActionPolicy _actionPolicy;
//    private readonly ICostPolicy _costPolicy;
//    private readonly ITargetingPolicy _targetingPolicy;
//    private readonly IEffectComputationPolicy _effectPolicy;
//    private readonly ICritPolicy _critPolicy;

//    public ActionResolver(
//        ICombatActionPolicy actionPolicy,
//        ICostPolicy costPolicy,
//        ITargetingPolicy targetingPolicy,
//        IEffectComputationPolicy effectPolicy,
//        ICritPolicy critPolicy)
//    {
//        _actionPolicy = actionPolicy;
//        _costPolicy = costPolicy;
//        _targetingPolicy = targetingPolicy;
//        _effectPolicy = effectPolicy;
//        _critPolicy = critPolicy;
//    }

//    public CombatActionResult Resolve(CombatActionChoice intent, Match match)
//    {
//        // --------------------------
//        // 1) Validate action legality
//        // --------------------------
//        var ctx = match.BuildContextFor(intent.ActorId);
//        var guard = _actionPolicy.EnsureActionIsValid(ctx, intent, match);
//        if (!guard.IsSuccess)
//            return CombatActionResult.Fail(intent, guard.Error!);

//        // -------------------
//        // 2) Pay energy cost
//        // -------------------
//        var costRes = _costPolicy.PayCost(intent, match);
//        if (!costRes.IsSuccess)
//            return CombatActionResult.Fail(intent, costRes.Error!);

//        // --------------------------
//        // 3) Determine final targets
//        // --------------------------
//        var targets = _targetingPolicy.ResolveTargets(intent, match);

//        // ----------------------------------------
//        // 4) Compute RAW effects (before crit)
//        // ----------------------------------------
//        var rawEffects = _effectPolicy.ComputeRawEffects(intent, targets, match);

//        // -----------------------------
//        // 5) Compute CRIT + modify raw
//        // -----------------------------
//        var critResult = _critPolicy.ApplyCrit(intent, rawEffects, ctx, match);

//        // -------------------------------------------------------
//        // 6) Build final raw CombatActionResult (NO mitigation!)
//        // -------------------------------------------------------
//        return CombatActionResult.Ok(
//            intent,
//            critResult.InstantEffects,
//            critResult.NewConditions,
//            critResult.WasCrit
//        );
//    }
//}
