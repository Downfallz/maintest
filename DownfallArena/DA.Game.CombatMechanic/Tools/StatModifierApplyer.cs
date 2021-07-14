using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System;

namespace DA.Game.CombatMechanic.Tools
{
    public class StatModifierApplyer : IStatModifierApplyer
    {
        public void ApplyEffect(StatModifier effect, Character character)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (character.IsDead)
                throw new Exception("Can't apply a stat modifier on a dead character.");

            switch (effect.StatType)
            {
                case Stats.Damage:
                    int total = effect.Modifier - character.BonusDefense;
                    if (total > 0)
                    {
                        int totalHp = character.Health - total;
                        if (totalHp <= 0)
                            character.Health = 0;
                        else
                            character.Health = totalHp;
                    }
                    break;
                case Stats.Critical:
                    double totalCrit = (double)effect.Modifier / 100 + character.BonusCritical;
                    if (totalCrit < 0)
                    {
                        character.BonusCritical = 0;
                    }
                    else if (totalCrit > 1)
                    {
                        character.BonusCritical = 1;
                    }
                    else
                    {
                        character.BonusCritical = totalCrit;
                    }
                    break;
                case Stats.Defense:
                    int totalDef = character.BonusDefense + effect.Modifier;
                    if (totalDef <= 0)
                        character.BonusDefense = 0;
                    else
                        character.BonusDefense = totalDef;
                    break;
                case Stats.Energy:
                    int totalEnergy = character.Energy + effect.Modifier;
                    if (totalEnergy <= 0)
                        character.Energy = 0;
                    else
                        character.Energy = totalEnergy;
                    break;
                case Stats.Health:
                    int totalHealth = character.Health + effect.Modifier;
                    if (totalHealth <= 0)
                    {
                        character.Health = 0;
                    }
                    else
                    {
                        if (totalHealth >= character.BaseHealth)
                            character.Health = character.BaseHealth;
                        else
                            character.Health = totalHealth;
                    }
                    break;
                case Stats.Initiative:
                    int totalInitiative = character.BonusInitiative + effect.Modifier;
                    if (totalInitiative < 0)
                        character.BonusInitiative = 0;
                    else
                        character.BonusInitiative = totalInitiative;
                    break;
                case Stats.Minions:
                    int totalExtraPoint = character.ExtraPoint + effect.Modifier;
                    if (totalExtraPoint < 0)
                        character.ExtraPoint = 0;
                    else
                        character.ExtraPoint = totalExtraPoint;
                    break;
                case Stats.Retaliate:
                    int totalRetaliate = character.BonusRetaliate + effect.Modifier;
                    if (totalRetaliate < 0)
                        character.BonusRetaliate = 0;
                    else
                        character.BonusRetaliate = totalRetaliate;
                    break;
                    break;
                case Stats.Stun:
                    character.IsStunned = true;
                    break;
            }
        }
    }
}
