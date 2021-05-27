using System.Collections.Generic;
using System.Linq;
using DA.Core.Abilities.Spells.Enum;

namespace DA.Core.Abilities.Spells.Entities
{
    public class Effect
    {
        public Effect(string name) : this(name, new List<StatModifier>()) { }

        public Effect(string name, IEnumerable<StatModifier> statModifiers) : this(name, statModifiers, EffectType.Direct) { }

        public Effect(string name, IEnumerable<StatModifier> statModifiers, EffectType effectType) : this(name, statModifiers, effectType, 0) { }

        public Effect(string name, IEnumerable<StatModifier> statModifiers, EffectType effectType, int length) : this(name, statModifiers, effectType, length, null, null) { }

        public Effect(string name, IEnumerable<StatModifier> statModifiers, EffectType effectType, int length,
            InitiativeEffectModifier lowInit, InitiativeEffectModifier highInit)
        {
            Name = name;
            StatModifiers = statModifiers.ToList().AsReadOnly();
            EffectType = effectType;
            Length = length;
            LowInitiativeModifier = lowInit;
            HighInitiativeModifier = highInit;
            
        }

        public int Length { get; }
        public InitiativeEffectModifier LowInitiativeModifier { get; }
        public InitiativeEffectModifier HighInitiativeModifier { get; }
        public string Name { get; }
        public EffectType EffectType { get; }
        public IReadOnlyList<StatModifier> StatModifiers { get; }
    }
}
