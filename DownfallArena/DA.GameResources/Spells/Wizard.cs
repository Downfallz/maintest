using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Wizard
    {
        public static Spell GetMeteor()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Wizard,
                Name = "Meteor",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 1,
                NbTargets = 3,
                CriticalChance = 0.5
            };

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
            Spell s = new Spell
            {
                CharacterClass = CharClass.Wizard,
                Name = "Ice Spear",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = 0.5
            };

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
            Spell s = new Spell
            {
                CharacterClass = CharClass.Wizard,
                Name = "Engulfing Flames",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.33
            };

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
