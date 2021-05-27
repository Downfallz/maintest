using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Battles.Mechanic.Abstractions;
using DA.Core.Domain.Base;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;
using DA.Core.Domain.Battles.Enum;

namespace DA.Core.Battles.Mechanic
{
    public class PlayerActionHandler : IPlayerActionHandler
    {
        private readonly IAppliedEffectManager _appliedEffectService;

        public PlayerActionHandler(IAppliedEffectManager appliedEffectService)
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

            var critInitModifier = 0;
            if (isCritical)
                critInitModifier++;
            if (isHigh)
                critInitModifier--;

            Console.WriteLine($"Speed is {init} and Critical is {isCritical}");

            foreach (Effect e in spell.Effects)
            {
                var ae = new AppliedEffect()
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
            var haveToResolve = spell.CriticalChance.HasValue && spell.CriticalChance.Value > 0;
            if (haveToResolve)
            {
                var totalCritChance = spell.CriticalChance.Value + source.BonusCritical;
                var rnd = new Random();
                var rndDouble = rnd.NextDouble();
                if (rndDouble < totalCritChance)
                {
                    return true;
                }
            }
            return false;
        }



    }
}