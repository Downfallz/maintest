using System;
using DA.Core.Battles.Mechanic.Abstractions;
using DA.Core.Domain.Base;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Battles.Mechanic
{
    public class StatModifierApplyer : IStatModifierApplyer
    {
        public void ApplyEffect(StatModifier effect, Character character)
        {
            if (character.IsDead)
                throw new Exception("Can't apply a stat modifier on a dead character.");

            switch (effect.StatType)
            {

                case Domain.Base.Talents.Enum.Stats.Damage:
                    var total = effect.Modifier - character.BonusDefense;
                    if (total > 0)
                    {
                        var totalHp = character.Health - total;
                        if (totalHp <= 0)
                            character.Health = 0;
                        else
                            character.Health = totalHp;
                    }
                    break;
                case Domain.Base.Talents.Enum.Stats.Critical:
                    character.BonusCritical += effect.Modifier;
                    break;
                case Domain.Base.Talents.Enum.Stats.Defense:
                    var totalDef = character.BonusDefense + effect.Modifier;
                    if (totalDef <= 0)
                        character.BonusDefense = 0;
                    else
                        character.BonusDefense = totalDef;
                    break;
                case Domain.Base.Talents.Enum.Stats.Energy:
                    var totalEnergy = character.Energy + effect.Modifier;
                    if (totalEnergy <= 0)
                        character.Energy = 0;
                    else
                        character.Energy = totalEnergy;
                    break;
                case Domain.Base.Talents.Enum.Stats.Health:
                    var totalHealth = character.Health + effect.Modifier;
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
                case Domain.Base.Talents.Enum.Stats.Initiative:
                    character.BonusInitiative += effect.Modifier;
                    break;
                case Domain.Base.Talents.Enum.Stats.Minions:
                    character.ExtraPoint += effect.Modifier;
                    break;
                case Domain.Base.Talents.Enum.Stats.Retaliate:
                    character.BonusRetaliate += effect.Modifier;
                    break;
                case Domain.Base.Talents.Enum.Stats.Stun:
                    character.IsStunned = true;
                    break;
            }
        }
    }
}
