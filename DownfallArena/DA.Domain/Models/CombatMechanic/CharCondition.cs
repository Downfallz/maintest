using DA.Game.Domain.Models.TalentsManagement.Spells;
using System;

namespace DA.Game.Domain.Models.CombatMechanic
{
    [Serializable]
    public class CharCondition
    {
        public bool IsPermanent { get; set; }
        public StatModifier StatModifier { get; set; }
        public int RoundsLeft { get; set; }

        public override string ToString()
        {
            string main = $"  [Condition - IsPermanent:{IsPermanent}  - RoundsLeft:{RoundsLeft}  - {StatModifier}]";

            return main;;
        }
    }
}
