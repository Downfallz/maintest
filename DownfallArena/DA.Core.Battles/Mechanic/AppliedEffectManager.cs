using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Battles.Mechanic.Abstractions;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;

namespace DA.Core.Battles.Mechanic
{
    public class AppliedEffectManager : IAppliedEffectManager
    {
        private readonly IStatModifierApplyer _statModifierService;

        public AppliedEffectManager(IStatModifierApplyer statModifierService)
        {
            _statModifierService = statModifierService;
        }

        public void ApplyEffect(AppliedEffect effect, Character source, List<Character> targets)
        {
            switch (effect.EffectType)
            {
                case Domain.Base.Talents.Enum.EffectType.Direct:
                    foreach(var t in targets)
                    {
                        if (!t.IsDead)
                            _statModifierService.ApplyEffect(effect.StatModifier, t);
                    }
                    
                    break;
                case Domain.Base.Talents.Enum.EffectType.SelfDirect:
                    _statModifierService.ApplyEffect(effect.StatModifier, source);
                    break;
                case Domain.Base.Talents.Enum.EffectType.Temporary:
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
                case Domain.Base.Talents.Enum.EffectType.SelfTemporary:
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
