using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services.CombatMechanic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.CombatMechanic
{
    public class SpellResolverService : ISpellResolverService
    {
        private readonly IAppliedEffectService _appliedEffectService;
        private readonly Random _rnd;

        public SpellResolverService(IAppliedEffectService appliedEffectService)
        {
            _appliedEffectService = appliedEffectService;
            _rnd = new Random();
        }

        public SpellResolverResult PlaySpell(Character source, Spell spell, List<Character> targets, Speed init)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));
            if (source.IsDead)
                throw new System.Exception("Can't apply an effect cast by a dead character");

            int energyCost = spell.EnergyCost.HasValue ? spell.EnergyCost.Value : 0;
            int minionsCost = spell.MinionsCost.HasValue? spell.MinionsCost.Value : 0;

            if (source.Energy < energyCost)
                throw new System.Exception("Not enough energy to cast spell");
            if (source.ExtraPoint < minionsCost)
                throw new System.Exception("Not enough minions/extra points to cast spell");

            var result = new SpellResolverResult();
            result.Spell = spell;
            result.SourceCharInfo = source.GetLightCharInfo();
            result.TargetsCharInfo = targets.Select(x => x.GetLightCharInfo()).ToList();
            result.Speed = init;
            result.InitialEnergy = source.Energy;
            result.InitialExtraPoint = source.ExtraPoint;

            bool isCritical = ResolveIsCritical(source, spell);
            bool isHigh = init == Speed.Quick;

            int critInitModifier = 0;
            if (isCritical)
                critInitModifier++;
            if (isHigh)
                critInitModifier--;

            result.IsCritical = isCritical;
            result.AppliedEffectResults = new List<AppliedEffectResult>();

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
                if (!source.IsDead)
                    result.AppliedEffectResults.Add(_appliedEffectService.ApplyEffect(ae, source, targets));
            }

            source.Energy -= energyCost;
            source.ExtraPoint -= minionsCost;

            result.PostEnergy = source.Energy;
            result.PostExtraPoint = source.ExtraPoint;
            return result;
        }

        private bool ResolveIsCritical(Character source, Spell spell)
        {
            bool haveToResolve = spell.CriticalChance.HasValue && spell.CriticalChance.Value > 0;
            if (haveToResolve)
            {
                double totalCritChance = spell.CriticalChance.Value + source.BonusCritical;
                
                double rndDouble = _rnd.NextDouble();
                if (rndDouble < totalCritChance)
                {
                    return true;
                }
            }
            return false;
        }
    }
}