using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System;

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
