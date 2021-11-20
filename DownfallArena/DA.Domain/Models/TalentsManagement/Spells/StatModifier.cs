using System;
using System.Text;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Domain.Models.TalentsManagement.Spells
{
    [Serializable]
    public class StatModifier
    {
        public Stats StatType { get; set; }
        public int Modifier { get; set; }

        public override string ToString()
        {
            return $"  ({Modifier} {StatType})  ";
        }
    }
}
