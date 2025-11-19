using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Resources.Creatures;

namespace DA.Game.Domain2.Matches.Entities;

public class CombatCharacter : Entity<CharacterId>
{
    protected CombatCharacter(CharacterId id) : base(id) { }

    public static CombatCharacter FromCharacterTemplate(CharacterDefinitionRef characterRuntimeTemplate, CharacterId id)
    {
        return new CombatCharacter(id)
        {
            BaseHealth = characterRuntimeTemplate.BaseHp,
            BaseEnergy = characterRuntimeTemplate.BaseEnergy,
            BaseDefense = characterRuntimeTemplate.BaseDefense,
            BaseInitiative = characterRuntimeTemplate.BaseInitiative,
            BaseCritical = characterRuntimeTemplate.BaseCritChance,
            StartingSpellIds = characterRuntimeTemplate.StartingSpellIds,
            Health = characterRuntimeTemplate.BaseHp,
            Energy = characterRuntimeTemplate.BaseEnergy,
            BonusDefense = characterRuntimeTemplate.BaseDefense,
            CurrentInitiative = characterRuntimeTemplate.BaseInitiative,
            BonusCritical = characterRuntimeTemplate.BaseCritChance,
            IsStunned = false
        };
    }
    IReadOnlyList<SpellId> StartingSpellIds { get; set; }
    public int BaseHealth { get; set; }
    public int BaseEnergy { get; set; }
    public int BaseDefense { get; set; }
    public int BaseInitiative { get; set; }
    public double BaseCritical { get; set; }
    public int Health { get; set; }
    public int Energy { get; set; }
    public int ExtraPoint { get; set; }
    public int BonusDefense { get; set; }
    public double BonusCritical { get; set; }
    public int CurrentInitiative { get; set; }
    public int BonusRetaliate { get; set; }
    public bool IsStunned { get; set; }

    public bool IsDead => Health <= 0;
    public bool IsAlive => !IsDead;
}


