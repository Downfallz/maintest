using System;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells
{
    [Serializable]
    public class StatModifier
    {
        public Stats StatType { get; set; }
        public int Modifier { get; set; }
    }
}
