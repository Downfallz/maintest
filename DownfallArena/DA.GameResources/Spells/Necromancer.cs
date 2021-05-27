using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Necromancer
    {
        public static Spell GetSummonMinions()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Necromancer;
            s.Name = "Summon Minions";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 0;
            s.CriticalChance = null;
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
            var s = new Spell();
            s.CharacterClass = CharClass.Necromancer;
            s.Name = "Revenant Guards";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 3;
            s.CriticalChance = 0.33;
            s.MinionsCost = 1;
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
            var s = new Spell();
            s.CharacterClass = CharClass.Necromancer;
            s.Name = "Crazed Specters";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 1;
            s.NbTargets = 3;
            s.CriticalChance = 0.33;
            s.MinionsCost = 1;
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
