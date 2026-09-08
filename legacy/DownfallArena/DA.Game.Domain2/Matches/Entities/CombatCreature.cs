using DA.Game.Domain2.Matches.Entities.Conditions;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Conditions;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain2.Matches.Entities;

public class CombatCreature : Entity<CreatureId>
{
    protected CombatCreature(CreatureId id) : base(id) { }

    public static CombatCreature FromCreatureTemplate(CreatureDefinitionRef creatureDefinitionTemplate, CreatureId id, PlayerSlot playerSlot)
    {
        ArgumentNullException.ThrowIfNull(creatureDefinitionTemplate);

        var creature = new CombatCreature(id)
        {
            BaseHealth = creatureDefinitionTemplate.BaseHp,
            BaseEnergy = creatureDefinitionTemplate.BaseEnergy,
            BaseDefense = creatureDefinitionTemplate.BaseDefense,
            BaseInitiative = creatureDefinitionTemplate.BaseInitiative,
            BaseCritical = creatureDefinitionTemplate.BaseCritChance,
            StartingSpellIds = creatureDefinitionTemplate.StartingSpellIds,
            Health = creatureDefinitionTemplate.BaseHp,
            Energy = creatureDefinitionTemplate.BaseEnergy,
            CurrentInitiative = creatureDefinitionTemplate.BaseInitiative,
            BonusDefense = Defense.Of(0),
            BonusCritical = CriticalChance.Of(0),
            IsStunned = false,
            OwnerSlot = playerSlot,
            TalentTreeId = creatureDefinitionTemplate.TalentTreeId
        };
        creature._knownSpellIds.AddRange(creatureDefinitionTemplate.StartingSpellIds);
        return creature;
    }

    public TalentTreeId? TalentTreeId { get; private set; }
    // Spells connus par la créature (starter + talents débloqués)
    public IReadOnlyList<SpellId> KnownSpellIds => _knownSpellIds.AsReadOnly();
    private readonly List<SpellId> _knownSpellIds = new();

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
    public PlayerSlot OwnerSlot { get; set; }

 

    public void UnlockSpell(SpellId spellId)
    {
        if (IsDead)
            return;

        if (!_knownSpellIds.Contains(spellId))
            _knownSpellIds.Add(spellId);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead)
            return;

        Health = Health.WithSubstracted(damage);
    }

    public void Heal(int heal)
    {
        if (IsDead)
            return;

        Health = Health.WithAdded(heal);
    }


    public void SpendOrLoseEnergy(int energy)
    {
        if (IsDead)
            return;

        Energy = Energy.WithSubstracted(energy);
    }

    public void GainEnergy(int energy)
    {
        if (IsDead)
            return;

        Energy = Energy.WithAdded(energy);
    }

    public void SpendOrLoseInitiative(int initiative)
    {
        if (IsDead)
            return;

        CurrentInitiative = CurrentInitiative.WithSubstracted(initiative);
    }

    public void GainInitiative(int initiative)
    {
        if (IsDead)
            return;

        CurrentInitiative = CurrentInitiative.WithAdded(initiative);
    }

    private readonly ConditionCollection _conditions = new();

    public ConditionCollection Conditions => _conditions;
    public int RetaliateValue { get; private set; }

    public void AddCondition(ConditionInstance condition)
        => _conditions.Add(condition);
}


