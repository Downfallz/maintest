using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public static class Trickster
    {
        public static Spell GetNoxiousCure()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Trickster,
                Name = "Noxious Cure",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 3,
                CriticalChance = 0.33
            };

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 3,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Defense,
                Modifier = -2,
                Length = 1
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public static Spell GetTranquilizerDart()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Trickster,
                Name = "Tranquilizer Dart",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = null
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
                EffectType = EffectType.Direct,
                Stats = Stats.Stun,
                Modifier = 1,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public static Spell GetInfectiousBlast()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Trickster,
                Name = "Infectious Blast",
                SpellType = SpellType.Offensive,
                EnergyCost = 1,
                Initiative = 2,
                NbTargets = 3,
                CriticalChance = null
            };

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Defense,
                Modifier = -2,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
