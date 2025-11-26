using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Resources.Stats;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests;

public static class CloneUtility
{
    public static CreatureSnapshot CloneSnapshot(
    CreatureSnapshot original,
    Health? health = null,
    bool? isStunned = null)
    {
        return new CreatureSnapshot(
            characterId: original.CharacterId,
            ownerSlot: original.OwnerSlot,
            health: health ?? original.Health,
            energy: original.Energy,
            initiative: original.Initiative,
            isStunned: isStunned ?? original.IsStunned,
            baseHealth: original.BaseHealth,
            baseEnergy: original.BaseEnergy,
            baseDefense: original.BaseDefense,
            baseInitiative: original.BaseInitiative,
            baseCritical: original.BaseCritical,
            bonusCritical: original.BonusCritical,
            bonusDefense: original.BonusDefense
        );
    }
}