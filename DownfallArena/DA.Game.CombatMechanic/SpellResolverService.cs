using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services.CombatMechanic;

namespace DA.Game.CombatMechanic
{
    public class SpellResolverService : ISpellResolverService
    {
        private readonly IAppliedEffectService _appliedEffectService;

        public SpellResolverService(IAppliedEffectService appliedEffectService)
        {
            _appliedEffectService = appliedEffectService;
        }

        public void PlaySpell(Character source, Spell spell, List<Character> targets, Speed init)
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            string targetNames = string.Join(", ", targets.Select(x => x.Name).ToList());
            Console.WriteLine($"{source.Name} plays {spell.Name} on {targetNames}");

            bool isCritical = ResolveIsCritical(source, spell);
            bool isHigh = init == Speed.Quick;

            int critInitModifier = 0;
            if (isCritical)
                critInitModifier++;
            if (isHigh)
                critInitModifier--;

            Console.WriteLine($"Speed is {init} and Critical is {isCritical}");

            foreach (Effect e in spell.Effects)
            {
                AppliedEffect ae = new AppliedEffect()
                {
                    EffectType = e.EffectType,
                    Length = e.Length,
                    StatModifier = new StatModifier()
                    {
                        StatType = e.Stats,
                        Modifier = e.Modifier + critInitModifier
                    }
                };
                _appliedEffectService.ApplyEffect(ae, source, targets);
            }

            source.Energy -= spell.EnergyCost.HasValue ? spell.EnergyCost.Value : 0;
        }

        private bool ResolveIsCritical(Character source, Spell spell)
        {
            bool haveToResolve = spell.CriticalChance.HasValue && spell.CriticalChance.Value > 0;
            if (haveToResolve)
            {
                double totalCritChance = spell.CriticalChance.Value + source.BonusCritical;
                Random rnd = new Random();
                double rndDouble = rnd.NextDouble();
                if (rndDouble < totalCritChance)
                {
                    return true;
                }
            }
            return false;
        }



    }
}