using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Trickster
    {
        public static Spell GetNoxiousCure()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Trickster;
            s.Name = "Noxious Cure";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 3;
            s.CriticalChance = 0.33;

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
            var s = new Spell();
            s.CharacterClass = CharClass.Trickster;
            s.Name = "Tranquilizer Dart";
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
            var s = new Spell();
            s.CharacterClass = CharClass.Trickster;
            s.Name = "Infectious Blast";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 1;
            s.Initiative = 2;
            s.NbTargets = 3;
            s.CriticalChance = null;

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
