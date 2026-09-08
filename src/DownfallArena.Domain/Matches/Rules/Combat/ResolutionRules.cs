using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Computes what an action does when its slot comes up, without touching the creatures: fizzles, dropped targets,
/// the critical roll, the energy cost, and one outcome per effect and target.
/// </summary>
public static class ResolutionRules
{
    public static CombatResolution Resolve(
        CombatAction action,
        IReadOnlyList<CreatureSnapshot> creatures,
        IGameResources resources,
        RuleSet rules,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);

        var actor = creatures.FirstOrDefault(candidate => candidate.Id == action.Actor)
            ?? throw new InvalidOperationException($"Actor {action.Actor} of a bound action is not in the match.");

        var canAct = IntentRules.CanAct(actor, action.Spell, resources);
        if (canAct.IsFailure)
        {
            return CombatResolution.Fizzle(action, canAct.Error);
        }

        var spell = resources.GetSpell(action.Spell);
        var report = TargetingRules.Check(actor, spell, action.Targets, creatures);
        if (report.GlobalFailures.FirstOrDefault() is { } global)
        {
            return CombatResolution.Fizzle(action, global.Error);
        }

        var invalid = report.InvalidTargets;
        var effectiveTargets = action.Targets.Where(target => !invalid.Contains(target)).ToList();
        if (effectiveTargets.Count == 0)
        {
            return CombatResolution.Fizzle(action, CombatErrors.AllTargetsInvalid);
        }

        var isCritical = random.NextDouble() < actor.CriticalChance.Plus(spell.Stats.CriticalChance.Value).Value;
        var multiplier = isCritical ? rules.CriticalMultiplier : 1.0;
        var outcomes = effectiveTargets
            .SelectMany(target => spell.Effects.Select(effect => Outcome(effect, creatures.First(candidate => candidate.Id == target), multiplier, isCritical)))
            .ToList();

        return CombatResolution.Resolved(action, effectiveTargets, [.. report.PerTargetFailures], isCritical, spell.Stats.Cost, outcomes);
    }

    private static EffectOutcome Outcome(Effect effect, CreatureSnapshot target, double multiplier, bool isCritical) =>
        effect switch
        {
            Damage damage => new DamageOutcome(target.Id, Math.Max(0, Multiplied(damage.Amount, multiplier) - target.TotalDefense.Value), isCritical),
            Heal heal => new HealOutcome(target.Id, heal.Amount),
            EnergyGain energy => new EnergyOutcome(target.Id, energy.Amount),
            LastingEffect lasting => new ConditionOutcome(target.Id, lasting),
            _ => throw new InvalidOperationException($"Effect '{effect.GetType().Name}' has no resolution rule."),
        };

    /// <summary>
    /// The floored product, capped at the largest damage a creature can take so a huge multiplier cannot overflow.
    /// </summary>
    private static int Multiplied(int amount, double multiplier) => (int)Math.Min(Math.Floor(amount * multiplier), int.MaxValue);
}
