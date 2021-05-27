using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams.Enum;

namespace DA.Core.Teams.GameResources.Data
{
    public static class Creature
    {
        public static Spell GetWait()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Creature;
            s.Name = "Wait";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 0;
            s.Initiative = 1;
            s.NbTargets = null;
            s.CriticalChance = null;
            s.Effects = new List<Effect>();
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 0;

            return s;
        }

        public static Spell GetAttack()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Creature;
            s.Name = "Strike";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 1;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 1,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 0;

            return s;
        }

        public static Spell GetSuperAttack()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Creature;
            s.Name = "Heavy Strike";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 0;

            return s;
        }
    }
}
