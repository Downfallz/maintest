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

    /// <summary>OngoingEffects sub-phase: bleeds deal their damage, which ignores defense.</summary>
    public static IReadOnlyList<BleedTick> OngoingEffects(IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        var ticks = new List<BleedTick>();
        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var bleeding = creature.Conditions.Select(condition => condition.Effect).OfType<Bleed>().Sum(bleed => bleed.AmountPerRound);
            if (bleeding > 0)
            {
                ticks.Add(new BleedTick(creature.Id, creature.TakeDamage(bleeding)));
            }
        }

        return ticks;
    }

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
