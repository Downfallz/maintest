using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Mercenary
    {
        public static Spell GetProtectiveSlam()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Mercenary,
                Name = "Protective Slam",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.333
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfDirect,
                Stats = Stats.Defense,
                Modifier = 1,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfTemporary,
                Stats = Stats.Defense,
                Modifier = 1,
                Length = 2
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetChainSlash()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Mercenary,
                Name = "Chain Slash",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 2,
                NbTargets = 2,
                CriticalChance = 0.5
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 5,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetThunderingSeal()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Mercenary,
                Name = "Thundering Seal",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = null
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
                EffectType = EffectType.Direct,
                Stats = Stats.Retaliate,
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
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Retaliate,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

    }
}
