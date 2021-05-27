using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Assassin
    {
        public static Spell GetMomentum()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Assassin;
            s.Name = "Momentum";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = null;
            s.Initiative = 3;
            s.NbTargets = null;
            s.CriticalChance = null;
            s.Level = 2;

            return s;
        }

        public static Spell GetDeathSquad()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Assassin;
            s.Name = "Death Squad";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 3;
            s.NbTargets = 3;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Initiative,
                Modifier = 10,
                Length = 1
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Critical,
                Modifier = 1,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetMortalWound()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Assassin;
            s.Name = "Mortal Wound";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
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
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = 2
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
