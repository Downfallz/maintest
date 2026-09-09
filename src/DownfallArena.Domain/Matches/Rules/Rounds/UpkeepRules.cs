using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// The automatic steps of a round: energy gain and ongoing effects at the start, condition countdown at the end.
/// </summary>
public static class UpkeepRules
{
    /// <summary>EnergyGain sub-phase: every living creature gains the rule set's energy per round.</summary>
    public static void EnergyGain(IReadOnlyList<Creature> creatures, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            creature.GainEnergy(rules.EnergyPerRound);
        }
    }

    /// <summary>
    /// OngoingEffects sub-phase: regenerations heal, then bleeds deal their damage, which ignores defense.
    /// Healing goes first on purpose (ADR 0019), so a regeneration can carry a creature through a bleed that
    /// would otherwise have killed it; the other order would make the two never meet.
    /// </summary>
    public static OngoingEffectTicks OngoingEffects(IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        var healed = new List<RegenerationTick>();
        var bled = new List<BleedTick>();
        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var regenerating = Total<Regeneration>(creature, regeneration => regeneration.AmountPerRound);
            if (regenerating > 0)
            {
                healed.Add(new RegenerationTick(creature.Id, creature.Heal(regenerating)));
            }
        }

        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var bleeding = Total<Bleed>(creature, bleed => bleed.AmountPerRound);
            if (bleeding > 0)
            {
                bled.Add(new BleedTick(creature.Id, creature.TakeDamage(bleeding)));
            }
        }

        return new OngoingEffectTicks(bled, healed);
    }

    private static int Total<TEffect>(Creature creature, Func<TEffect, int> amount)
        where TEffect : LastingEffect =>
        creature.Conditions.Select(condition => condition.Effect).OfType<TEffect>().Sum(amount);

    /// <summary>Cleanup sub-phase: every condition counts one round down; the expired ones are returned per creature.</summary>
    public static IReadOnlyDictionary<CreatureId, IReadOnlyList<Condition>> Cleanup(IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        var expired = new Dictionary<CreatureId, IReadOnlyList<Condition>>();
        foreach (var creature in creatures)
        {
            var gone = creature.TickConditions();
            if (gone.Count > 0)
            {
                expired[creature.Id] = gone;
            }
        }

        return expired;
    }
}
