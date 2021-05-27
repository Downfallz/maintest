using System;
using DA.Core.Battles.Mechanic.Abstractions;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Battles.Mechanic
{
    public class CharacterCondManager : ICharacterCondManager
    {
        private readonly IStatModifierApplyer _statModifierService;

        public CharacterCondManager(IStatModifierApplyer statModifierService)
        {
            _statModifierService = statModifierService;
        }

        public void ApplyCondition(CharCondition charCond, Character target)
        {
            if (!target.IsDead)
            {
                switch (charCond.StatModifier.StatType)
                {
                    case Domain.Base.Talents.Enum.Stats.Damage:
                    case Domain.Base.Talents.Enum.Stats.Health:
                    case Domain.Base.Talents.Enum.Stats.Energy:
                        ApplyRecurring(charCond, target);
                        break;
                    default:
                        ApplyPassive(charCond);
                        break;
                }
            }            
        }

        private void ApplyRecurring(CharCondition charCond, Character target)
        {
            _statModifierService.ApplyEffect(charCond.StatModifier, target);
            DecreaseRoundLeft(charCond);
        }
        private void ApplyPassive(CharCondition charCond)
        {
            DecreaseRoundLeft(charCond);
        }

        private void DecreaseRoundLeft(CharCondition charCond)
        {
            charCond.RoundsLeft--;
        }
    }
}
