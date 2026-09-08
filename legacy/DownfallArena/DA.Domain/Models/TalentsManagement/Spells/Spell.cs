using System;
using System.Collections.Generic;
using System.Text;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Domain.Models.TalentsManagement.Spells
{
    [Serializable]
    public class Spell
    {
        public Spell()
        {
            Effects = new List<Effect>();
            PassiveEffects = new List<PassiveEffect>();
        }

        public int? EnergyCost { get; set; }
        public string Name { get; set; }
        public SpellType SpellType { get; set; }
        public CharClass CharacterClass { get; set; }
        public List<Effect> Effects { get; set; }
        public int? NbTargets { get; set; }
        public int Initiative { get; set; }
        public int? MinionsCost { get; set; }
        public double? CriticalChance { get; set; }
        public List<PassiveEffect> PassiveEffects { get; set; }
        public int Level { get; set; }
        public override string ToString()
        {
            string main = $"{Name} eng:{EnergyCost} init:{Initiative} crit:{CriticalChance} (";
            StringBuilder sb = new StringBuilder();
            sb.Append(main);
            foreach (Effect e in Effects)
            {
                sb.Append($"|{e.EffectType} {e.Stats} {e.Modifier} {e.Length}|");
            }

            sb.Append(")");

            return sb.ToString();
        }
    }
}
