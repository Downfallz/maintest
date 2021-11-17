using DA.Game.CombatMechanic.Tools;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services.CombatMechanic;

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
                    foreach (Character t in targets)
                    {
                        if (!t.IsDead)
                            _statModifierService.ApplyEffect(effect.StatModifier, t);
                    }

                    break;
                case EffectType.SelfDirect:
                    _statModifierService.ApplyEffect(effect.StatModifier, source);
                    break;
                case EffectType.Temporary:
                    foreach (Character t in targets)
                    {
                        if (!t.IsDead)
                        {
                            CharCondition charCond = new CharCondition
                            {
                                StatModifier = effect.StatModifier,
                                IsPermanent = false,
                                RoundsLeft = effect.Length.Value
                            };
                            t.CharConditions.Add(charCond);
                        }
                    }
                    break;
                case EffectType.SelfTemporary:
                    CharCondition charCond2 = new CharCondition
                    {
                        StatModifier = effect.StatModifier,
                        IsPermanent = false,
                        RoundsLeft = effect.Length.Value
                    };

                    source.CharConditions.Add(charCond2);
                    break;
            }
        }
    }
}
