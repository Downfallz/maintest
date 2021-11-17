using DA.Game.CombatMechanic.Tools;
using System;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services.CombatMechanic;

namespace DA.Game.CombatMechanic
{
    public class CharacterCondService : ICharacterCondService
    {
        private readonly IStatModifierApplyer _statModifierService;

        public CharacterCondService(IStatModifierApplyer statModifierService)
        {
            _statModifierService = statModifierService;
        }

        public void ApplyCondition(CharCondition charCond, Character target)
        {
            if (charCond == null)
                throw new ArgumentNullException(nameof(charCond));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (charCond.StatModifier == null)
                throw new ArgumentException("charCond must have a stat modifier.", nameof(charCond));
            if (!target.IsDead)
            {
                switch (charCond.StatModifier.StatType)
                {
                    case Stats.Damage:
                    case Stats.Health:
                    case Stats.Energy:
                        _statModifierService.ApplyEffect(charCond.StatModifier, target);
                        break;
                }
            }

            charCond.RoundsLeft--;
        }
    }
}
