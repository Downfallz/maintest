//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DA.Game.Domain2.Matches.Services.Combat;

//public sealed class DamagePolicyV1 : IDamagePolicy
//{
//    public int ComputeFinalDamage(
//        int rawDamage,
//        CreatureSnapshot attacker,
//        CreatureSnapshot defender,
//        MatchSnapshot match)
//    {
//        if (defender.IsDead)
//            return 0;

//        // Rulebook v1: Defense fully mitigates damage
//        var mitigated = Math.Max(0, rawDamage - defender.Defense);

//        return mitigated;
//    }
//}
