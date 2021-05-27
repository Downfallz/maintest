using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams.Enum;

namespace DA.Core.Teams.GameResources.Data
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
