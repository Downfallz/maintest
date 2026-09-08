using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Applies a resolution to the creatures: the actor pays the cost, every outcome hits its target.
/// </summary>
public static class CombatExecution
{
    public static void Apply(CombatResolution resolution, IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(creatures);

        if (resolution.Fizzled)
        {
            return;
        }

        var actor = Find(resolution.Action.Actor, creatures);
        var paid = actor.SpendEnergy(resolution.EnergySpent);
        if (paid.IsFailure)
        {
            throw new InvalidOperationException($"Actor {actor.Id} cannot pay a cost the resolution accepted: {paid.Error.Message}");
        }

        foreach (var outcome in resolution.Outcomes)
        {
            var target = Find(outcome.Target, creatures);
            switch (outcome)
            {
                case DamageOutcome damage:
                    target.TakeDamage(damage.Amount);
                    break;
                case HealOutcome heal:
                    target.Heal(heal.Amount);
                    break;
                case EnergyOutcome energy:
                    target.GainEnergy(energy.Amount);
                    break;
                case ConditionOutcome condition:
                    target.Apply(condition.Effect);
                    break;
                default:
                    throw new InvalidOperationException($"Outcome '{outcome.GetType().Name}' has no execution rule.");
            }
        }
    }

    private static Creature Find(CreatureId id, IReadOnlyList<Creature> creatures) =>
        creatures.FirstOrDefault(creature => creature.Id == id)
            ?? throw new InvalidOperationException($"Creature {id} of a resolution is not in the match.");
}
