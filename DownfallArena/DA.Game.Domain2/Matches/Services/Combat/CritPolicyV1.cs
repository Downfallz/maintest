//using DA.Game.Domain2.Matches.Contexts;
//using DA.Game.Domain2.Matches.ValueObjects;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DA.Game.Domain2.Matches.Services.Combat;

///// <summary>
///// Crit policy V1: no crit, just passes through the raw effects.
///// </summary>
//public sealed class CritPolicyV1 : ICritPolicy
//{
//    public CritComputationResult ApplyCrit(
//        CombatActionChoice intent,
//        RawEffectBundle raw,
//        PlayerActionContext ctx,
//        Match match)
//    {
//        // V1: no crit logic, simply forward raw bundle
//        return CritComputationResult.FromRaw(raw, wasCritical: false);
//    }
//}