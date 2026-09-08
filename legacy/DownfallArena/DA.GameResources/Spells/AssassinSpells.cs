using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Resources.Spells
{
    public class AssassinSpells : IAssassinSpells
    {
        public Spell GetDeathSquad()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Assassin,
                Name = "Death Squad",
                SpellType = SpellType.Defensive,
                EnergyCost = 2,
                Initiative = 3,
                NbTargets = 3,
                CriticalChance = null
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Initiative,
                Modifier = 10,
                Length = 1
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Temporary,
                Stats = Stats.Critical,
                Modifier = 1,
                Length = 1
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
        public Spell GetMomentum()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Assassin,
                Name = "Momentum",
                SpellType = SpellType.Defensive,
                EnergyCost = null,
                Initiative = 3,
                NbTargets = null,
                CriticalChance = null,
                Level = 2
            };

            return s;
        }

        public Spell GetMortalWound()
        {
            Spell s = new Spell
            {
                CharacterClass = CharClass.Assassin,
                Name = "Mortal Wound",
                SpellType = SpellType.Offensive,
                EnergyCost = 3,
                Initiative = 2,
                NbTargets = 1,
                CriticalChance = 0.5
            };
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = null
            });
            s.Effects.Add(new Effect()
            {
                EffectType = EffectType.Direct,
                Stats = Stats.Damage,
                Modifier = 4,
                Length = 2
            });
            s.PassiveEffects = new List<PassiveEffect>();
            s.Level = 3;

            return s;
        }
    }
}
