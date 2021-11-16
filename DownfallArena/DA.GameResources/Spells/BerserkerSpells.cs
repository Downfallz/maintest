using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public class BerserkerSpells : IBerserkerSpells
    {
        public Spell GetEnragedCharge()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Berserker,
                Name = "Enraged Charge",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = null
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
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public Spell GetTornado()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Berserker,
                Name = "Tornado",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 3,
                CriticalChance = 0.33
            };
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

        public Spell GetPsychoRush()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Berserker,
                Name = "Psycho Rush",
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
