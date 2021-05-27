using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;

namespace DA.Core.Domain.Base.Teams
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
        public int Initiative
        {
            get { return CharacterTalentStats.Initiative + BonusInitiative; }
        }
        public List<CharCondition> CharConditions { get; set; }
        public List<Spell> UnlockedSpells
        {
            get
            {
                return CharacterTalentStats == null
                    ? new List<Spell>()
                    : CharacterTalentStats.Spells.ToList();
            }
        }

        public List<Spell> CastableSpells
        {
            get
            {
                return UnlockedSpells.Where(x => x.EnergyCost <= Energy).ToList();
            }
        }

        public override string ToString()
        {
            string main = $"[{Name} {Health}/{BaseHealth} - {Initiative} initiative - {Energy} energy]";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(main);
            foreach (var s in CharacterTalentStats.UnlockedTalents)
            {
                sb.AppendLine($"    {s.Name} ");
            }

            return sb.ToString();
        }
        
        public bool IsDead => Health <= 0;
    }
}
