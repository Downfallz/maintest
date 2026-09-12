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
    /// OngoingEffects sub-phase: energy regenerations give their energy, regenerations heal, then bleeds deal
    /// their damage, which ignores defense. Healing goes before the bleeds on purpose (ADR 0019), so a
    /// regeneration can carry a creature through a bleed that would otherwise have killed it; the other order
    /// would make the two never meet.
    /// <para>
    /// Energy goes first, and that is a choice rather than a no-op: each loop skips the dead, so a creature its
    /// own bleed kills this round still gained its energy and still reports a tick. What the position cannot
    /// change is any health number or who is left standing.
    /// </para>
    /// </summary>
    public static OngoingEffectTicks OngoingEffects(IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        var gained = new List<EnergyRegenerationTick>();
        var healed = new List<RegenerationTick>();
        var bled = new List<BleedTick>();
        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var asked = Asked<EnergyRegeneration>(creature, energyRegeneration => energyRegeneration.AmountPerRound);
            if (asked.Total > 0)
            {
                var given = creature.GainEnergy(asked.Total);
                gained.Add(new EnergyRegenerationTick(creature.Id, given, Shared(asked.Wanted, given)));
            }
        }

        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var asked = Asked<Regeneration>(creature, regeneration => regeneration.AmountPerRound);
            if (asked.Total > 0)
            {
                var given = creature.Heal(asked.Total);
                healed.Add(new RegenerationTick(creature.Id, given, Shared(asked.Wanted, given)));
            }
        }

        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            var asked = Asked<Bleed>(creature, bleed => bleed.AmountPerRound);
            if (asked.Total > 0)
            {
                var taken = creature.TakeDamage(asked.Total);
                bled.Add(new BleedTick(creature.Id, taken, Shared(asked.Wanted, taken)));
            }
        }

        return new OngoingEffectTicks(gained, healed, bled);
    }

    /// <summary>
    /// What one creature's conditions of a kind ask for this round: the total the rules apply, and what each
    /// spell behind it asked for (ADR 0027). The total is summed exactly as it was before the shares existed,
    /// so nothing about the health arithmetic depends on this reading.
    /// </summary>
    private static (int Total, IReadOnlyList<ConditionShare> Wanted) Asked<TEffect>(
        Creature creature,
        Func<TEffect, int> amount)
        where TEffect : LastingEffect
    {
        var wanted = new List<ConditionShare>();
        var total = 0;
        foreach (var condition in creature.Conditions)
        {
            if (condition.Effect is not TEffect effect)
            {
                continue;
            }

            var asked = amount(effect);
            total += asked;
            if (condition.Source is not null && asked > 0)
            {
                wanted.Add(new ConditionShare(condition.Source, asked));
            }
        }

        return (total, wanted);
    }

    /// <summary>
    /// What each spell is answerable for once the board has had its say, by largest remainder so the shares
    /// add up to exactly what happened rather than to what was asked (ADR 0027).
    /// <para>
    /// A tick the board took in full is handed back unchanged. A tick cut short -- two points of bleed on a
    /// creature with one point of health -- is split in proportion, and the point that cannot be halved goes
    /// to the largest remainder, then to the largest ask, then to the first spell in ordinal id order, so two
    /// equal claims resolve the same way every time.
    /// </para>
    /// </summary>
    private static IReadOnlyList<ConditionShare> Shared(IReadOnlyList<ConditionShare> wanted, int happened)
    {
        var asked = wanted.Sum(share => share.Amount);
        if (wanted.Count == 0 || happened <= 0 || asked == 0)
        {
            return [];
        }

        if (happened >= asked)
        {
            return wanted;
        }

        var shares = wanted
            .Select(share => new { share.Source, Exact = (double)share.Amount * happened / asked, share.Amount })
            .Select(share => new { share.Source, share.Exact, share.Amount, Whole = (int)Math.Floor(share.Exact) })
            .ToList();

        var left = happened - shares.Sum(share => share.Whole);
        var extra = shares
            .OrderByDescending(share => share.Exact - share.Whole)
            .ThenByDescending(share => share.Amount)
            .ThenBy(share => share.Source.Spell.Value, StringComparer.Ordinal)
            .Take(left)
            .Select(share => share.Source)
            .ToHashSet();

        return [.. shares
            .Select(share => new ConditionShare(share.Source, share.Whole + (extra.Contains(share.Source) ? 1 : 0)))
            .Where(share => share.Amount > 0)];
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
