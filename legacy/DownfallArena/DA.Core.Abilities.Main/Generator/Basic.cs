using System.Collections.Generic;
using DA.Core.Abilities.Spells.Entities;
using DA.Core.Abilities.Spells.Enum;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Main.Generator
{
    public static class Basic
    {
        private const SpellArchetype SpellArchetype = Core.Abilities.Spells.Enum.SpellArchetype.Base;
        public static Talent GetAttack()
        {
            var listStatModifier = new List<StatModifier>();
            listStatModifier.Add(new StatModifier(){Stats = Stats.Health, Modifier = -10});

            var lowInit = new InitiativeEffectModifier();
            lowInit.StaticEffectModifier = new EffectModifier();
            lowInit.StaticEffectModifier.OperationType = OperationType.Divide;
            lowInit.StaticEffectModifier.Modifier = 2;

            lowInit.RandomEffectModifier = new RandomEffectModifier();
            lowInit.RandomEffectModifier.DiceRollType = DiceRollType.D6;
            lowInit.RandomEffectModifier.NumberEqualOrHigher = 4;
            lowInit.RandomEffectModifier.OperationType = OperationType.Times;
            lowInit.RandomEffectModifier.Modifier = 2;

            var highInit = new InitiativeEffectModifier();

            highInit.RandomEffectModifier = new RandomEffectModifier();
            highInit.RandomEffectModifier.DiceRollType = DiceRollType.D6;
            highInit.RandomEffectModifier.NumberEqualOrHigher = 4;
            highInit.RandomEffectModifier.OperationType = OperationType.Times;
            highInit.RandomEffectModifier.Modifier = 2;

            var listEffect = new List<Effect>();
            var eff = new Effect("Damage", listStatModifier, EffectType.Direct, 0, lowInit, highInit);

            var s = new Spell("Attack", 2, SpellType.Offensive, SpellArchetype, new List<Effect>(){ eff });
            var t = new Talent("Attack", s);
            return t;
        }

        public static Talent GetSuperAttack()
        {
            var listStatModifier = new List<StatModifier>();
            listStatModifier.Add(new StatModifier() { Stats = Stats.Health, Modifier = -15 });

            var lowInit = new InitiativeEffectModifier();
            lowInit.StaticEffectModifier = new EffectModifier();
            lowInit.StaticEffectModifier.OperationType = OperationType.Substract;
            lowInit.StaticEffectModifier.Modifier = 5;

            lowInit.RandomEffectModifier = new RandomEffectModifier();
            lowInit.RandomEffectModifier.DiceRollType = DiceRollType.D6;
            lowInit.RandomEffectModifier.NumberEqualOrHigher = 4;
            lowInit.RandomEffectModifier.OperationType = OperationType.Add;
            lowInit.RandomEffectModifier.Modifier = 5;

            var highInit = new InitiativeEffectModifier();

            highInit.RandomEffectModifier = new RandomEffectModifier();
            highInit.RandomEffectModifier.DiceRollType = DiceRollType.D6;
            highInit.RandomEffectModifier.NumberEqualOrHigher = 4;
            highInit.RandomEffectModifier.OperationType = OperationType.Times;
            highInit.RandomEffectModifier.Modifier = 2;

            var listEffect = new List<Effect>();
            var eff = new Effect("Damage", listStatModifier, EffectType.Direct, 0, lowInit, highInit);

            var s = new Spell("Heavy Attack", 4, SpellType.Offensive, SpellArchetype.Base, new List<Effect>() { eff });
            var t = new Talent("Heavy Attack", s);
            return t;
        }

        public static Talent GetDefend()
        {
            var listStatModifier = new List<StatModifier>();
            listStatModifier.Add(new StatModifier() { Stats = Stats.Defense, Modifier = 5 });

            var listEffect = new List<Effect>();
            var eff = new Effect("Defense", listStatModifier, EffectType.Temporary, 1);

            var s = new Spell("Defend", 2, SpellType.Defensive, SpellArchetype.Base, new List<Effect>() { eff });
            var t = new Talent("Defend", s);
            return t;
        }

        public static Talent GetWait()
        {
            var listStatModifier = new List<StatModifier>();
            listStatModifier.Add(new StatModifier() { Stats = Stats.Defense, Modifier = 0 });

            var listEffect = new List<Effect>();
            var eff = new Effect("Wait", listStatModifier);

            var s = new Spell("Wait", 0, SpellType.Defensive, SpellArchetype, new List<Effect>() { eff });
            var t = new Talent("Wait", s);
            return t;
        }
    }
}
