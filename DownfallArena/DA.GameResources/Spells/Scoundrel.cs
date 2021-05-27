using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public static class Scoundrel
    {
        public static Spell GetPoisonSlash()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Scoundrel;
            s.Name = "Poison Slash";
            s.SpellType = SpellType.Defensive;
            s.EnergyCost = 2;
            s.Initiative = 1;
            s.NbTargets = 1;
            s.CriticalChance = null;
           
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Damage,
                Modifier = 1,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();

            s.Level = 1;
            return s;
        }

        public static Spell GetThrowingStar()
        {
            var s = new Spell();
            s.CharacterClass = CharClass.Scoundrel;
            s.Name = "Throwing Star";
            s.SpellType = SpellType.Offensive;
            s.EnergyCost = 1;
            s.Initiative = 2;
            s.NbTargets = 1;
            s.CriticalChance = null;

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }
    }
}
