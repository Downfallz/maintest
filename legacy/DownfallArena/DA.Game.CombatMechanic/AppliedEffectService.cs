using DA.Game.CombatMechanic.Tools;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services.CombatMechanic;
using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.CombatMechanic
{
    public class AppliedEffectService : IAppliedEffectService
    {
        private readonly IStatModifierApplyer _statModifierService;

        public AppliedEffectService(IStatModifierApplyer statModifierService)
        {
            _statModifierService = statModifierService;
        }

        public AppliedEffectResult ApplyEffect(AppliedEffect effect, Character source, List<Character> targets)
        {
            // source is null when effect come from environment (ie.: round energy)
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (effect.StatModifier == null)
                throw new ArgumentException("charCond must have a stat modifier.", nameof(effect.StatModifier));
            if (source != null && source.IsDead)
                throw new System.Exception("Can't apply an effect cast by a dead character");

            var result = new AppliedEffectResult();
            result.Effect = effect;
            result.CharCondResults = new List<CharCondAddResult>();
            result.StatResults = new List<StatModifierResult>();

            switch (effect.EffectType)
            {
                case EffectType.Direct:
                    foreach (Character t in targets)
                    {
                        if (!t.IsDead)
                            result.StatResults.Add(_statModifierService.ApplyEffect(effect.StatModifier, t));
                    }

                    break;
                case EffectType.SelfDirect:
                    result.StatResults.Add(_statModifierService.ApplyEffect(effect.StatModifier, source));
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
                                RoundsLeft = effect.Length ?? 0
                            };
                            t.CharConditions.Add(charCond);
                            result.CharCondResults.Add(new CharCondAddResult()
                            {
                                CharCondition = charCond,
                                TargetCharacterId = t.Id,
                                TargetCharacterName = t.Name,
                                TargetCharacterTeam = t.TeamNumber
                            });
                        }
                    }
                    break;
                case EffectType.SelfTemporary:
                    CharCondition charCond2 = new CharCondition
                    {
                        StatModifier = effect.StatModifier,
                        IsPermanent = false,
                        RoundsLeft = effect.Length ?? 0
                    };

                    source.CharConditions.Add(charCond2);
                    result.CharCondResults.Add(new CharCondAddResult()
                    {
                        CharCondition = charCond2,
                        TargetCharacterId = source.Id,
                        TargetCharacterName = source.Name,
                        TargetCharacterTeam = source.TeamNumber
                    });
                    break;
            }
            return result;
        }
    }
}
