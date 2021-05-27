using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams.Enum;

namespace DA.Core.Teams.GameResources.Data
{
    public static class Wizard
    {
        public static Spell GetMeteor()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Wizard;
            s.Name = "Meteor";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 1;
            s.NbTargets = 3;
            s.CriticalChance = 0.5;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetIceSpear()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Wizard;
            s.Name = "Ice Spear";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = 0.5;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Initiative,
                Modifier = -2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetEngulfingFlames()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Wizard;
            s.Name = "Engulfing Flames";
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

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
