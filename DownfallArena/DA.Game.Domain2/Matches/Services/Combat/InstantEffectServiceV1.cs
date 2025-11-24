//using DA.Game.Shared.Contracts.Matches.Enums;
//using DA.Game.Shared.Contracts.Matches.Ids;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DA.Game.Domain2.Matches.Services.Combat;

//public sealed class InstantEffectServiceV1 : IInstantEffectService
//{
//    private readonly IDamagePolicy _damagePolicy;

//    public InstantEffectServiceV1(IDamagePolicy damagePolicy)
//    {
//        _damagePolicy = damagePolicy;
//    }

//    public void ApplyInstantEffect(Match match, Creature target, InstantEffectApplication eff)
//    {
//        if (target.IsDead)
//            return;

//        switch (eff.Kind)
//        {
//            case EffectKind.Damage:
//                ApplyDamage(target, eff.Amount);
//                break;

//            case EffectKind.Health:
//                ApplyHeal(target, eff.Amount);
//                break;

//            case EffectKind.Energy:
//                ApplyEnergy(target, eff.Amount);
//                break;

//            case EffectKind.Defense:
//                ApplyDefenseBuff(target, eff.Amount);
//                break;

//            case EffectKind.Initiative:
//                ApplyInitiativeBuff(target, eff.Amount);
//                break;

//            case EffectKind.Retaliate:
//                ApplyRetaliate(target, eff.Amount);
//                break;

//            case EffectKind.Stun:
//                ApplyStun(target);
//                break;

//            case EffectKind.Critical:
//                ApplyCriticalBuff(target, eff.Amount);
//                break;
//        }
//    }

//    private void ApplyDamage(Match match, Creature target, InstantEffectApplication eff)
//    {
//        var attacker = match.GetCreatureSnapshot(eff.SourceId);
//        var defender = match.GetCreatureSnapshot(target.Id);

//        var finalDamage = _damagePolicy.ComputeFinalDamage(
//            eff.Amount,
//            attacker,
//            defender,
//            match.ToSnapshot()
//        );

//        if (finalDamage > 0)
//            target.TakeDamage(finalDamage);
//    }
//}
