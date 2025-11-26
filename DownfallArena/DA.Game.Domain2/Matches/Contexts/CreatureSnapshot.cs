using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Utilities;


namespace DA.Game.Domain2.Matches.Contexts;

public sealed record CreatureSnapshot : ValueObject
{
    public CreatureId CharacterId { get; private set; }
    public PlayerSlot OwnerSlot { get; private set; }
    public Health Health { get; private set; }
    public Energy Energy { get; private set; }
    public Initiative Initiative { get; private set; }
    public bool IsStunned { get; private set; }

    public Health BaseHealth { get; private set; }
    public Energy BaseEnergy { get; private set; }
    public Defense BaseDefense { get; private set; }
    public Initiative BaseInitiative { get; private set; }
    public CriticalChance BaseCritical { get; private set; }

    public CriticalChance BonusCritical { get; private set; }
    public Defense BonusDefense { get; private set; }

    public bool IsAlive => !Health.IsDead();
    public bool IsDead => !IsAlive;

    public Defense TotalDefense => Defense.Of(BaseDefense.Value + BonusDefense.Value);

    public CreatureSnapshot(
        CreatureId characterId,
        PlayerSlot ownerSlot,
        Health health,
        Energy energy,
        Initiative initiative,
        bool isStunned,
        Health baseHealth,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCritical,
        CriticalChance bonusCritical,
        Defense bonusDefense)
    {
        CharacterId = characterId;
        OwnerSlot = ownerSlot;
        Health = health;
        Energy = energy;
        Initiative = initiative;
        IsStunned = isStunned;
        BaseHealth = baseHealth;
        BaseEnergy = baseEnergy;
        BaseDefense = baseDefense;
        BaseInitiative = baseInitiative;
        BaseCritical = baseCritical;
        BonusCritical = bonusCritical;
        BonusDefense = bonusDefense;
    }

    public static CreatureSnapshot From(CombatCreature character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return new CreatureSnapshot(
            character.Id,
            character.OwnerSlot,            
            character.Health,
            character.Energy,
            character.CurrentInitiative,
            character.IsStunned,
            character.BaseHealth,
            character.BaseEnergy,
            character.BaseDefense,
            character.BaseInitiative,
            character.BaseCritical,
            character.BonusCritical,
            character.BonusDefense
        );
    }
}

