using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public class CreatureSpells : ICreatureSpells
    {
        public Spell GetWait()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Creature,
                Name = "Wait",
                SpellType = SpellType.Defensive,
                EnergyCost = 0,
                Initiative = 1,
                NbTargets = null,
                CriticalChance = null,
                Effects = new List<Effect>(),
                PassiveEffects = new List<PassiveEffect>(),
                Level = 0
            };

            return s;
        }

        public Spell GetAttack()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Creature,
                Name = "Strike",
                SpellType = SpellType.Offensive,
                EnergyCost = 1,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = null
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 1,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 0;

            return s;
        }

        public Spell GetSuperAttack()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Creature,
                Name = "Heavy Strike",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 1,
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
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 0;

            return s;
        }
    }
}
