using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Shaman
    {
        public static Spell GetHealingScreech()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Shaman;
            s.Name = "Healing Screech";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.5;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Health,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;
            return s;
        }

        public static Spell GetToxicWaves()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Shaman;
            s.Name = "Toxic Waves";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 2;
            s.NbTargets = 3;
            s.CriticalChance = 0.33;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetRestoringBurst()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Shaman;
            s.Name = "Restoring Burst";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = null;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 3,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Energy,
                Modifier = 2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
