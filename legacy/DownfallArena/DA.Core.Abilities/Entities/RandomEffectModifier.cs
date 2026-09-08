using DA.Core.Abilities.Spells.Enum;

namespace DA.Core.Abilities.Spells.Entities
{
    public class RandomEffectModifier : EffectModifier
    {
        public DiceRollType DiceRollType { get; set; }
        public int NumberEqualOrHigher { get; set; }
    }
}
