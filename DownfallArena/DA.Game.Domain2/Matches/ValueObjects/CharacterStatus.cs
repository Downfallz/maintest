using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CharacterStatus : ValueObject
{
    public CharacterId CharacterId { get; private set; }
    public Health Health { get; private set; }
    public Energy Energy { get; private set; }
    public Initiative Initiative { get; private set; }
    public bool IsStunned { get; private set; }
    public Health BaseHealth { get; private set; }
    public Energy BaseEnergy { get; private set; }
    public Defense BaseDefense { get; private set; }
    public Initiative BaseInitiative { get; private set; }
    public CriticalChance BaseCritical { get; private set; }
    public bool IsAlive => !Health.IsDead();
    private CharacterStatus(
        CharacterId characterId,
        Health health,
        Energy energy,
        Initiative initiative,
        bool isStunned,
        Health baseHealth,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCritical)
    {
        CharacterId = characterId;
        Health = health;
        Energy = energy;
        Initiative = initiative;
        IsStunned = isStunned;
        BaseHealth = baseHealth;
        BaseEnergy = baseEnergy;
        BaseDefense = baseDefense;
        BaseInitiative = baseInitiative;
        BaseCritical = baseCritical;
    }

    public static CharacterStatus From(CombatCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return new CharacterStatus(
            character.Id,
            character.Health,
            character.Energy,
            character.CurrentInitiative,
            character.IsStunned,
            character.BaseHealth,
            character.BaseEnergy,
            character.BaseDefense,
            character.BaseInitiative,
            character.BaseCritical
        );
    }
}
