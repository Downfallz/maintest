using System;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class CharCondition
    {
        public bool IsPermanent { get; set; }
        public StatModifier StatModifier { get; set; }
        public int RoundsLeft { get; set; }
    }
}
