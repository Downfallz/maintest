using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams.Enum;

namespace DA.Core.Teams.GameResources.Data
{
    public static class Berserker
    {
        public static Spell GetEnragedCharge()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Berserker;
            s.Name = "Enraged Charge";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetTornado()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Berserker;
            s.Name = "Tornado";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 3;
            s.CriticalChance = 0.33;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetPsychoRush()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Berserker;
            s.Name = "Psycho Rush";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.33;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 9,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfTemporary,
                Stats = Stats.Defense,
                Modifier = -2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
