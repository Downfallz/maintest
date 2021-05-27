using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Brawler
    {
        public static Spell GetPummel()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Brawler;
            s.Name = "Pummel";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 1;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.667;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }

        public static Spell GetGuard()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Brawler;
            s.Name = "Guard";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 1;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Defense,
                Modifier = 1,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Defense,
                Modifier = 1,
                Length = 2
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }
    }
}
