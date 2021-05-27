using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Warlord
    {
        public static Spell GetFullPlate()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Warlord;
            s.Name = "Full Plate";
            s.SpellType = SpellType.Passive;
            s.EnergyCost = null;
            s.Initiative = 1;
            s.NbTargets = null;
            s.CriticalChance = null;

            s.PassiveEffects.Add(new PassiveEffect()
            {
                StatModifier = new StatModifier()
                {
                    StatType = Stats.Defense,
                    Modifier = 1
                }
            });
            s.Level = 2;
            return s;
        }

        public static Spell GetCrushingStomp()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Warlord;
            s.Name = "Crushing Stomp";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 4;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = 0.667;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 6,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Stun,
                Modifier = 1,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetRestorativeGush()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Warlord;
            s.Name = "Restorative Gush";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = 0.17;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 6,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
