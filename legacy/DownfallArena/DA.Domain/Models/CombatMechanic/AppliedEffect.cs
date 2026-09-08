using System;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Domain.Models.CombatMechanic
{
    [Serializable]
    public class AppliedEffect
    {
        public int? Length { get; set; }
        public EffectType EffectType { get; set; }
        public StatModifier StatModifier { get; set; }
    }
}
