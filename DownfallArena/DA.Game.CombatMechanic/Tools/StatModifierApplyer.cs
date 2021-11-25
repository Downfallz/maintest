using System;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services.CombatMechanic;

namespace DA.Game.CombatMechanic.Tools
{
    public class StatModifierApplyer : IStatModifierApplyer
    {
        public StatModifierResult ApplyEffect(StatModifier effect, Character character)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (character.IsDead)
                throw new Exception("Can't apply a stat modifier on a dead character.");

            var result = new StatModifierResult();
            result.Effect = effect;
            result.TargetCharacterId = character.Id;
            result.TargetCharacterName = character.Name;
            result.TargetCharacterTeam = character.TeamNumber;

            switch (effect.StatType)
            {
                case Stats.Damage:
                    result.TargetModifier = -character.BonusDefense;
                    result.TargetModifierName = nameof(character.BonusDefense);
                    int totalDmg = effect.Modifier - character.BonusDefense;
                    if (totalDmg < 0)
                        totalDmg = 0;
                    result.TotalEffectiveValue = totalDmg;
                    result.PreEffectStatsValue = character.Health;

                    int totalHp = character.Health - totalDmg;
                    if (totalHp <= 0)
                        character.Health = 0;
                    else
                        character.Health = totalHp;

                    result.PostEffectStatsValue = character.Health;
                    break;
                case Stats.Critical:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    double total = (double)effect.Modifier / 100;
                    result.TotalEffectiveValue = total;
                    result.PreEffectStatsValue = character.BonusCritical;

                    double finalValueCrit = total + character.BonusCritical;
                    if (finalValueCrit < 0)
                    {
                        character.BonusCritical = 0;
                    }
                    else if (finalValueCrit > 1)
                    {
                        character.BonusCritical = 1;
                    }
                    else
                    {
                        character.BonusCritical = finalValueCrit;
                    }

                    result.PostEffectStatsValue = character.BonusCritical;
                    break;
                case Stats.Defense:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;

                    int totalDef = effect.Modifier;
                    result.TotalEffectiveValue = totalDef;
                    result.PreEffectStatsValue = character.BonusDefense;
                    int finalValueDef = totalDef + character.BonusDefense;

                    if (finalValueDef <= 0)
                        character.BonusDefense = 0;
                    else
                        character.BonusDefense = finalValueDef;

                    result.PostEffectStatsValue = character.BonusDefense;
                    break;
                case Stats.Energy:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalEnergy = effect.Modifier;
                    result.TotalEffectiveValue = totalEnergy;
                    result.PreEffectStatsValue = character.Energy;

                    int finalValueEnergy = character.Energy + effect.Modifier;
                    if (finalValueEnergy <= 0)
                        character.Energy = 0;
                    else
                        character.Energy = finalValueEnergy;

                    result.PostEffectStatsValue = character.Energy;
                    break;
                case Stats.Health:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalHealth = effect.Modifier;
                    result.TotalEffectiveValue = totalHealth;
                    result.PreEffectStatsValue = character.Health;
                    int finalValueHealth = character.Health + effect.Modifier;

                    if (finalValueHealth <= 0)
                    {
                        character.Health = 0;
                    }
                    else
                    {
                        if (finalValueHealth >= character.BaseHealth)
                            character.Health = character.BaseHealth;
                        else
                            character.Health = finalValueHealth;
                    }

                    result.PostEffectStatsValue = character.Health;
                    break;
                case Stats.Initiative:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalInitiative = effect.Modifier;
                    result.TotalEffectiveValue = totalInitiative;
                    result.PreEffectStatsValue = character.BonusInitiative;
                    int finalValueInitiative = character.BonusInitiative + effect.Modifier;
                    if (finalValueInitiative < 0)
                        character.BonusInitiative = 0;
                    else
                        character.BonusInitiative = finalValueInitiative;
                    result.PostEffectStatsValue = character.BonusInitiative;
                    break;
                case Stats.Minions:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalExtraPoint = effect.Modifier;
                    result.TotalEffectiveValue = totalExtraPoint;
                    result.PreEffectStatsValue = character.ExtraPoint;
                    int finalValueExtraPoint = character.ExtraPoint + effect.Modifier;
                    if (finalValueExtraPoint < 0)
                        character.ExtraPoint = 0;
                    else
                        character.ExtraPoint = finalValueExtraPoint;
                    result.PostEffectStatsValue = character.ExtraPoint;
                    break;
                case Stats.Retaliate:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalRetaliate = effect.Modifier;
                    result.TotalEffectiveValue = totalRetaliate;
                    result.PreEffectStatsValue = character.BonusRetaliate;
                    int finalValueRetaliate = character.BonusRetaliate + effect.Modifier;
                    if (finalValueRetaliate < 0)
                        character.BonusRetaliate = 0;
                    else
                        character.BonusRetaliate = finalValueRetaliate;
                    result.PostEffectStatsValue = character.BonusRetaliate;
                    break;
                case Stats.Stun:
                    result.TargetModifier = 0;
                    result.TargetModifierName = string.Empty;
                    int totalStun = 1;
                    result.TotalEffectiveValue = totalStun;
                    result.PreEffectStatsValue = character.IsStunned ? 1 : 0;
                    character.IsStunned = true;
                    result.PostEffectStatsValue = character.IsStunned ? 1 : 0;
                    break;
            }
            return result;
        }

    }
}
