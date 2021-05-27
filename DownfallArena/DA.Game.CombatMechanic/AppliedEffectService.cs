using System.Collections.Generic;
using DA.Game.CombatMechanic.Tools;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services.GameFlowEngine.CombatMechanic;

namespace DA.Game.CombatMechanic
{
    public class AppliedEffectService : IAppliedEffectService
    {
        private readonly IStatModifierApplyer _statModifierService;

        public AppliedEffectService(IStatModifierApplyer statModifierService)
        {
            _statModifierService = statModifierService;
        }

        public void ApplyEffect(AppliedEffect effect, Character source, List<Character> targets)
        {
            switch (effect.EffectType)
            {
                case EffectType.Direct:
                    foreach(var t in targets)
                    {
                        if (!t.IsDead)
                            _statModifierService.ApplyEffect(effect.StatModifier, t);
                    }
                    
                    break;
                case EffectType.SelfDirect:
                    _statModifierService.ApplyEffect(effect.StatModifier, source);
                    break;
                case EffectType.Temporary:
                    foreach (var t in targets)
                    {
                        if (!t.IsDead)
                        {
                            var charCond = new CharCondition();
                            charCond.StatModifier = effect.StatModifier;
                            charCond.IsPermanent = false;
                            charCond.RoundsLeft = effect.Length.Value;
                            t.CharConditions.Add(charCond);
                        }
                    }
                    break;
                case EffectType.SelfTemporary:
                    var charCond2 = new CharCondition();
                    charCond2.StatModifier = effect.StatModifier;
                    charCond2.IsPermanent = false;
                    charCond2.RoundsLeft = effect.Length.Value;

                    source.CharConditions.Add(charCond2);
                    break;
            }
        }
    }
}
