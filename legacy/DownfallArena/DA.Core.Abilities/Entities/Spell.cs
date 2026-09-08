using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Abilities.Spells.Enum;
using DA.Core.Abilities.Talents.Abstractions;

namespace DA.Core.Abilities.Spells.Entities
{
    public class Spell : ISpell, ITalentBonus
    {
        public Spell()
        {
        }

        public Spell(string name, int castingCost, SpellType spellType, SpellArchetype spellArchetype, IEnumerable<Effect> effects)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty");
            if (castingCost < 0)
                throw new ArgumentOutOfRangeException(nameof(castingCost), "Casting cost must be higher than 0.");
            if (effects == null || !effects.Any())
                throw new ArgumentException("A spell must at least have one effect.");

            Name = name;
            CastingCost = castingCost;
            SpellType = spellType;
            Effects = effects.ToList().AsReadOnly();
        }

        public int CastingCost { get; }
        public string Name { get; }
        public SpellType SpellType { get; }
        public IReadOnlyList<Effect> Effects { get; }
        public override string ToString()
        {
            return Name;
        }
    }
}
