using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells
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
            string main = $"[{Name} {EnergyCost} energy - {Initiative} initiative - {CriticalChance} critChance]";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(main);
            foreach (Effect e in Effects)
            {
                sb.AppendLine($"    {e.EffectType} {e.Stats} {e.Modifier} {e.Length} ");
            }

            return sb.ToString();
        }
    }
}
