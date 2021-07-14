using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class AppliedEffect
    {
        public int? Length { get; set; }
        public EffectType EffectType { get; set; }
        public StatModifier StatModifier { get; set; }
    }
}
