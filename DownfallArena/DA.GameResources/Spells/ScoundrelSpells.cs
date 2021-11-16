using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;

namespace DA.Game.Resources.Spells
{
    public class ScoundrelSpells : IScoundrelSpells
    {
        public Spell GetPoisonSlash()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Scoundrel,
                Name = "Poison Slash",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = null
            };

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

        public Spell GetThrowingStar()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Scoundrel,
                Name = "Throwing Star",
                SpellType = SpellType.Offensive,
                EnergyCost = 1,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = null
            };

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
