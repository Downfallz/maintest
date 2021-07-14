using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Models.GameFlowEngine
{
    [Serializable]
    public class Character
    {
        public Guid Id { get; }
        public Character()
        {
            Id = Guid.NewGuid();
            Name = "Creature";
            CharConditions = new List<CharCondition>();
        }
        public int TeamNumber { get; set; }
        public string Name { get; set; }
        public int BaseHealth { get; set; }
        public TalentTreeStructure TalentTreeStructure { get; set; }
        public CharacterTalentStats CharacterTalentStats { get; set; }
        public int Health { get; set; }
        public int Energy { get; set; }
        public int ExtraPoint { get; set; }
        public int BonusAttackPower { get; set; }
        public int BonusDefense { get; set; }
        public double BonusCritical { get; set; }
        public int BonusInitiative { get; set; }
        public int BonusRetaliate { get; set; }
        public bool IsStunned { get; set; }
        public int Initiative => CharacterTalentStats.Initiative + BonusInitiative;
        public List<CharCondition> CharConditions { get; set; }

        public override string ToString()
        {
            string main = $"[{Name} {Health}/{BaseHealth} - {Initiative} initiative - {Energy} energy]";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(main);
            foreach (TalentsManagement.Spells.Spell s in CharacterTalentStats.UnlockedSpells)
            {
                sb.AppendLine($"    {s.Name} ");
            }

            return sb.ToString();
        }

        public bool IsDead => Health <= 0;
    }
}
