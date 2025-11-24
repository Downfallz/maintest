using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
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

    //public void TakeDamage(int rawDamage)
    //{
    //    if (IsDead)
    //        return;

    //    var mitigated = Math.Max(0, rawDamage - Defense.Value);
    //    if (mitigated <= 0)
    //        return;

    //    Health = Health.WithSubtracted(mitigated); // clamp à 0, max 20 dans le VO
    //    if (Health.Value <= 0)
    //        IsDead = true;
    //}

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


