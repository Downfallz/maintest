using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public class LeechSpells : ILeechSpells
    {
        public Spell GetParasiteJab()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Leech,
                Name = "Parasite Jab",
                SpellType = SpellType.Offensive,
                EnergyCost = 2,
                Initiative = 1,
                NbTargets = 1,
                CriticalChance = 0.5
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
                EffectType = EffectType.SelfDirect,
                Stats = Stats.Health,
                Modifier = 2,
                Length = null
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 2;

            return s;
        }

        public Spell GetHatefulSacrifice()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Leech,
                Name = "Hateful Sacrifice",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 3,
                NbTargets = 1,
                CriticalChance = 0.5
            };
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

        public Spell GetSoulDevourer()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Leech,
                Name = "Soul Devourer",
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
