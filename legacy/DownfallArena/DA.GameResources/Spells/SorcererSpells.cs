using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public class SorcererSpells : ISorcererSpells
    {
        public Spell GetLightningBolt()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Sorcerer,
                Name = "Lightning Bolt",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.667
            };

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 3,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }

        public Spell GetRejuvenate()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Sorcerer,
                Name = "Rejuvenate",
                SpellType = SpellType.Defensive,
                EnergyCost = 1,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.17
            };

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 3,
                Length = null
            });

            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 1;

            return s;
        }
    }
}
