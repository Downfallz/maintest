using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain2.Matches.Entities;

public class CombatCharacter : Entity<CreatureId>
{
    protected CombatCharacter(CreatureId id) : base(id) { }

    public static CombatCharacter FromCharacterTemplate(CharacterDefinitionRef characterRuntimeTemplate, CreatureId id)
    {
        ArgumentNullException.ThrowIfNull(characterRuntimeTemplate);

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
    public required IReadOnlyList<SpellId> StartingSpellIds { get; set; }
    public required Health BaseHealth { get; set; }
    public required Energy BaseEnergy { get; set; }
    public required Defense BaseDefense { get; set; }
    public required Initiative BaseInitiative { get; set; }
    public required CriticalChance BaseCritical { get; set; }
    public required Health Health { get; set; }
    public required Energy Energy { get; set; }
    public required Defense BonusDefense { get; set; }
    public required CriticalChance BonusCritical { get; set; }
    public required Initiative CurrentInitiative { get; set; }
    public required bool IsStunned { get; set; }
    public bool IsDead => Health.IsDead();
    public bool IsAlive => !IsDead;
}


