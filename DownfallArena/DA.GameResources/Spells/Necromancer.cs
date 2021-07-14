using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Necromancer
    {
        public static Spell GetSummonMinions()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Necromancer,
                Name = "Summon Minions",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 0,
                CriticalChance = null
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfDirect,
                Stats = Stats.Minions,
                Modifier = 6,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetRevenantGuards()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Necromancer,
                Name = "Revenant Guards",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 3,
                CriticalChance = 0.33,
                MinionsCost = 1
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Defense,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Defense,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetCrazedSpecters()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Necromancer,
                Name = "Crazed Specters",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 1,
                NbTargets = 3,
                CriticalChance = 0.33,
                MinionsCost = 1
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 6,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
