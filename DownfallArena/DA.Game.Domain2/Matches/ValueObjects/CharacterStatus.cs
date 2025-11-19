using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record CharacterStatus : ValueObject
{
    public CharacterId CharacterId { get; private set; }
    public int Health { get; private set; }
    public int Energy { get; private set; }
    public int Initiative { get; private set; }
    public bool IsStunned { get; private set; }
    public bool IsAlive => Health > 0;
    public int BaseHealth { get; private set; }
    public int BaseEnergy { get; private set; }
    public int BaseDefense { get; private set; }
    public int BaseInitiative { get; private set; }
    public double BaseCritical { get; private set; }

    private CharacterStatus(CharacterId characterId, int health, int energy, int initiative, bool isStunned)
    {
        CharacterId = characterId;
        Health = health;
        Energy = energy;
        Initiative = initiative;
        IsStunned = isStunned;
    }

    public static CharacterStatus From(CombatCharacter character)
    {
        return new CharacterStatus(character.Id,
            character.Health,
            character.Energy,
            character.CurrentInitiative,
            character.IsStunned)
        {
            BaseHealth = character.BaseHealth,
            BaseEnergy = character.BaseEnergy,
            BaseDefense = character.BaseDefense,
            BaseInitiative = character.BaseInitiative,
            BaseCritical = character.BaseCritical   
        };
    }
}
