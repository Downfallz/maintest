using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System;

namespace DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells
{
    [Serializable]
    public class Effect
    {
        public int? Length { get; set; }
        public EffectType EffectType { get; set; }
        public Stats Stats { get; set; }
        public int Modifier { get; set; }
    }
}
