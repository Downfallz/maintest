using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain.Tests.Matches.Policies;

internal static class CloneUtility
{
    public static CreatureSnapshot CloneSnapshot(
        CreatureSnapshot original,
        Health? health = null,
        bool? isStunned = null,
        Energy? energy = null,
        PlayerSlot? ownerSlot = null,
        CriticalChance? baseCritical = null,
        CriticalChance? bonusCritical = null)
    {
        return new CreatureSnapshot(
            characterId: original.CharacterId,
            ownerSlot: ownerSlot ?? original.OwnerSlot,
            health: health ?? original.Health,
            energy: energy ?? original.Energy,
            initiative: original.Initiative,
            isStunned: isStunned ?? original.IsStunned,
            baseHealth: original.BaseHealth,
            baseEnergy: original.BaseEnergy,
            baseDefense: original.BaseDefense,
            baseInitiative: original.BaseInitiative,
            baseCritical: baseCritical ?? original.BaseCritical,
            bonusCritical: bonusCritical ?? original.BonusCritical,
            bonusDefense: original.BonusDefense
        );
    }
}

