using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams.Enum;

namespace DA.Core.Teams.GameResources.Data
{
    public static class Scoundrel
    {
        public static Spell GetPoisonSlash()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Scoundrel;
            s.Name = "Poison Slash";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
           
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Damage,
                Modifier = 1,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();

            s.Level = 1;
            return s;
        }

        public static Spell GetThrowingStar()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Scoundrel;
            s.Name = "Throwing Star";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 1;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = null;

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
    }
}
