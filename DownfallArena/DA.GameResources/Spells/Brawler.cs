using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Brawler
    {
        public static Spell GetPummel()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Brawler,
                Name = "Pummel",
                SpellType = SpellType.Offensive,
                EnergyCost = 1,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.667
            };
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
            Spell s = new Spell
            {
                CharacterClass = CharClass.Brawler,
                Name = "Guard",
                SpellType = SpellType.Defensive,
                EnergyCost = 1,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = null
            };
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
