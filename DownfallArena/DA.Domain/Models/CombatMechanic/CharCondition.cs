using System;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.CombatMechanic
{
    [Serializable]
    public class CharCondition
    {
        public bool IsPermanent { get; set; }
        public StatModifier StatModifier { get; set; }
        public int RoundsLeft { get; set; }
    }
}
