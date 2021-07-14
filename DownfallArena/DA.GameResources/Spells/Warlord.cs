using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Warlord
    {
        public static Spell GetFullPlate()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Warlord,
                Name = "Full Plate",
                SpellType = SpellType.Passive,
                EnergyCost = null,
                Initiative = 1,
                NbTargets = null,
                CriticalChance = null
            };

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
            Spell s = new Spell
            {
                CharacterClass = CharClass.Warlord,
                Name = "Crushing Stomp",
                SpellType = SpellType.Offensive,
                EnergyCost = 4,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.667
            };

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
            Spell s = new Spell
            {
                CharacterClass = CharClass.Warlord,
                Name = "Restorative Gush",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = 0.17
            };

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
