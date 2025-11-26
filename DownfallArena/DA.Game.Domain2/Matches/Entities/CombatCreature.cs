using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain2.Matches.Entities;

public class CombatCreature : Entity<CreatureId>
{
    protected CombatCreature(CreatureId id) : base(id) { }

    public static CombatCreature FromCreatureTemplate(CreatureDefinitionRef creatureDefinitionTemplate, CreatureId id, PlayerSlot playerSlot)
    {
        ArgumentNullException.ThrowIfNull(creatureDefinitionTemplate);

        return new CombatCreature(id)
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
            OwnerSlot = playerSlot
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
    public PlayerSlot OwnerSlot { get; set; }

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

    //private readonly List<ActiveCondition> _conditions = new();

    //public IReadOnlyList<ActiveCondition> Conditions => _conditions;

    //public void AddCondition(ConditionDefinition def)
    //{
    //    // Do not apply stun twice if already stunned
    //    if (def.StatKind == EffectKind.Stun && IsStunned)
    //        return;

    //    if (!def.IsStackable)
    //    {
    //        var existing = _conditions.FirstOrDefault(c => c.Definition.Name == def.Name);
    //        if (existing is not null)
    //            return;
    //    }

    //    _conditions.Add(new ActiveCondition(def));
    //}

    //public void ResolveConditionsAtRoundStart()
    //{
    //    var expired = new List<ActiveCondition>();

    //    foreach (var cond in _conditions)
    //    {
    //        // OverTime: apply per-tick effect
    //        if (cond.Definition.Type == ConditionType.OverTime)
    //        {
    //            ApplyOverTimeEffect(cond.Definition);
    //        }

    //        cond.TickRound();

    //        if (cond.IsExpired)
    //            expired.Add(cond);
    //    }

    //    foreach (var c in expired)
    //        _conditions.Remove(c);
    //}

    //private void ApplyOverTimeEffect(ConditionDefinition def)
    //{
    //    switch (def.StatKind)
    //    {
    //        case EffectKind.Damage:
    //            TakeDamage(def.AmountPerTick);
    //            break;
    //        case EffectKind.Health:
    //            Heal(def.AmountPerTick);
    //            break;
    //            // etc. si tu veux des DoT sur Energy, etc.
    //    }
    //}


}


