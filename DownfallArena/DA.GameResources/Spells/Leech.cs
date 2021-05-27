using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Leech
    {
        public static Spell GetParasiteJab()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Leech;
            s.Name = "Parasite Jab";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.5;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfDirect,
                Stats = Stats.Health,
                Modifier = 2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetHatefulSacrifice()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Leech;
            s.Name = "Hateful Sacrifice";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 3;
            s.NbTargets = 1;
            s.CriticalChance = 0.5;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 10,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.SelfDirect,
                Stats = Stats.Health,
                Modifier = -4,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetSoulDevourer()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Leech;
            s.Name = "Soul Devourer";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 3;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = null;
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Energy,
                Modifier = -2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
