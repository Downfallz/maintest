using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Sorcerer
    {
        public static Spell GetLightningBolt()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Sorcerer;
            s.Name = "Lightning Bolt";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.667;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }

        public static Spell GetRejuvenate()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Sorcerer;
            s.Name = "Rejuvenate";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 1;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.17;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 3,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }
    }
}
