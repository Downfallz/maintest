using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Mercenary
    {
        public static Spell GetProtectiveSlam()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Mercenary;
            s.Name = "Protective Slam";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.333;
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
            var s = new Spell();
            s.CharacterClass = CharClass.Mercenary;
            s.Name = "Chain Slash";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 2;
            s.NbTargets = 2;
            s.CriticalChance = 0.5;
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
            var s = new Spell();
            s.CharacterClass = CharClass.Mercenary;
            s.Name = "Thundering Seal";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = null;
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
