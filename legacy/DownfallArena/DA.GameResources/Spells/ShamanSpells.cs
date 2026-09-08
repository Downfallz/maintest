using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public class ShamanSpells : IShamanSpells
    {
        public Spell GetHealingScreech()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Shaman,
                Name = "Healing Screech",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.5
            };

            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Health,
                Modifier = 2,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Health,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;
            return s;
        }

        public Spell GetToxicWaves()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Shaman,
                Name = "Toxic Waves",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 2,
                NbTargets = 3,
                CriticalChance = 0.33
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
                EffectType = EffectType.Temporary,
                Stats = Stats.Damage,
                Modifier = 2,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }

        public Spell GetRestoringBurst()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Shaman,
                Name = "Restoring Burst",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = null
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
                EffectType = EffectType.Direct,
                Stats = Stats.Energy,
                Modifier = 2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
