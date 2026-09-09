using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Applies a resolution to the creatures: the actor pays the cost, every outcome hits its target.
/// </summary>
public static class CombatExecution
{
    /// <summary>
    /// Applies the resolution and answers with what the board actually took, which is not always what the rules
    /// computed: damage is capped by the health left, healing by the health missing, a dead creature takes
    /// neither, and a condition can be refused by its stacking policy. The outcomes come back in the same shape
    /// they went in, carrying the applied amount, and an outcome that changed nothing is left out.
    /// </summary>
    public static IReadOnlyList<EffectOutcome> Apply(CombatResolution resolution, IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(creatures);

        if (resolution.Fizzled)
        {
            return [];
        }

        var actor = Find(resolution.Action.Actor, creatures);
        var paid = actor.SpendEnergy(resolution.EnergySpent);
        if (paid.IsFailure)
        {
            throw new InvalidOperationException($"Actor {actor.Id} cannot pay a cost the resolution accepted: {paid.Error.Message}");
        }

        var applied = new List<EffectOutcome>();
        foreach (var outcome in resolution.Outcomes)
        {
            var landed = Land(outcome, Find(outcome.Target, creatures));
            if (landed is not null)
            {
                applied.Add(landed);
            }
        }

        return applied;
    }

    private static EffectOutcome? Land(EffectOutcome outcome, Creature target) => outcome switch
    {
        DamageOutcome damage => Amounted(target.TakeDamage(damage.Amount), dealt => damage with { Amount = dealt }),
        HealOutcome heal => Amounted(target.Heal(heal.Amount), healed => heal with { Amount = healed }),
        EnergyOutcome energy => Amounted(target.GainEnergy(energy.Amount), gained => energy with { Amount = gained }),
        ConditionOutcome condition => target.Apply(condition.Effect) is null ? null : condition,
        _ => throw new InvalidOperationException($"Outcome '{outcome.GetType().Name}' has no execution rule."),
    };

    private static EffectOutcome? Amounted(int amount, Func<int, EffectOutcome> of) => amount > 0 ? of(amount) : null;

    private static Creature Find(CreatureId id, IReadOnlyList<Creature> creatures) =>
        creatures.FirstOrDefault(creature => creature.Id == id)
            ?? throw new InvalidOperationException($"Creature {id} of a resolution is not in the match.");
}
