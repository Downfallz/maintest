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
            var landed = ApplyTo(outcome, Find(outcome.Target, creatures));
            if (landed is not null)
            {
                applied.Add(landed);
            }
        }

        // A copy: the caller hands this to an event a recorder keeps for the whole match.
        return [.. applied];
    }

    /// <summary>
    /// One outcome against one creature, answering with what it did, or <c>null</c> when it did nothing. An
    /// amount of zero is nothing whether the creature refused it or the rules computed it that way.
    /// </summary>
    private static EffectOutcome? ApplyTo(EffectOutcome outcome, Creature target)
    {
        EffectOutcome? landed;
        switch (outcome)
        {
            case DamageOutcome damage:
                var dealt = target.TakeDamage(damage.Amount);
                landed = dealt > 0 ? damage with { Amount = dealt } : null;
                break;
            case HealOutcome heal:
                var healed = target.Heal(heal.Amount);
                landed = healed > 0 ? heal with { Amount = healed } : null;
                break;
            case EnergyOutcome energy:
                var gained = target.GainEnergy(energy.Amount);
                landed = gained > 0 ? energy with { Amount = gained } : null;
                break;
            case ConditionOutcome condition:
                landed = target.Apply(condition.Effect) is null ? null : condition;
                break;
            default:
                throw new InvalidOperationException($"Outcome '{outcome.GetType().Name}' has no execution rule.");
        }

        return landed;
    }

    private static Creature Find(CreatureId id, IReadOnlyList<Creature> creatures) =>
        creatures.FirstOrDefault(creature => creature.Id == id)
            ?? throw new InvalidOperationException($"Creature {id} of a resolution is not in the match.");
}
