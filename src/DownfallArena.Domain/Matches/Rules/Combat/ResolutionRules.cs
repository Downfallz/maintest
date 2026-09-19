using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Computes what an action does when its slot comes up, without touching the creatures: fizzles, dropped targets,
/// the critical roll, the energy cost, and one outcome per effect and target.
/// <para>
/// One roll decides the cast, and it multiplies what the cast puts on a target's health *now*: `Damage` and
/// `Heal` (ADR 0033). Not a lasting effect, whose payout is spread over rounds the one roll should not decide;
/// not an effect on the caster (ADR 0031); and not energy, which is another economy.
/// </para>
/// </summary>
public static class ResolutionRules
{
    public static CombatResolution Resolve(
        CombatAction action,
        IReadOnlyList<CreatureSnapshot> creatures,
        IGameResources resources,
        RuleSet rules,
        IRandomSource random,
        Speed speed)
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

        // The roll is drawn whatever the speed, so that a Quick cast consumes the same randomness a Standard
        // one does: the choice changes the outcome, not the stream every later cast in the match reads from.
        var isCritical = random.NextDouble() < CriticalChanceOf(actor, spell, speed);
        var multiplier = isCritical ? rules.CriticalMultiplier : 1.0;
        var outcomes = effectiveTargets
            .SelectMany(target => spell.Effects.Select(effect => Outcome(effect, creatures.First(candidate => candidate.Id == target), multiplier, isCritical)))
            .ToList();

        // What the cast does to whoever cast it (ADR 0031): once, however many targets it reached, and never
        // multiplied by the critical roll -- a recoil that doubles when the blow lands well is another idea.
        // Marked, because the target alone cannot say: a Self-targeted spell puts ordinary outcomes here too.
        outcomes.AddRange(spell.CasterEffects.Select(effect => Outcome(effect, actor, multiplier: 1.0, isCritical) with { OnCaster = true }));

        return CombatResolution.Resolved(action, effectiveTargets, [.. report.PerTargetFailures], isCritical, spell.Stats.Cost, outcomes);
    }

    /// <summary>
    /// What a cast's chance of a critical is: the creature's and the spell's added, and <b>zero when the
    /// creature chose Quick</b>. Speed is a trade rather than a free ordering — a Quick slot acts before every
    /// Standard one and pays the critical roll for it — so the chance belongs here, where the rule is, and not
    /// at each of the three places that price a cast. An agent asking what a candidate is worth must read it
    /// from here too, or it will price a crit it cannot roll.
    /// </summary>
    public static double CriticalChanceOf(CreatureSnapshot actor, Spell spell, Speed speed)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spell);

        return speed == Speed.Quick ? 0 : actor.CriticalChance.Plus(spell.Stats.CriticalChance.Value).Value;
    }

    private static EffectOutcome Outcome(Effect effect, CreatureSnapshot target, double multiplier, bool isCritical) =>
        effect switch
        {
            Damage damage => new DamageOutcome(target.Id, Math.Max(0, Multiplied(damage.Amount, multiplier) - target.TotalDefense.Value), isCritical),
            Heal heal => new HealOutcome(target.Id, Multiplied(heal.Amount, multiplier)),
            EnergyGain energy => new EnergyOutcome(target.Id, energy.Amount),
            EnergyDrain drain => new EnergyDrainOutcome(target.Id, drain.Amount),
            LastingEffect lasting => new ConditionOutcome(target.Id, lasting),
            _ => throw new InvalidOperationException($"Effect '{effect.GetType().Name}' has no resolution rule."),
        };

    /// <summary>
    /// The floored product, capped at the largest damage a creature can take so a huge multiplier cannot overflow.
    /// </summary>
    private static int Multiplied(int amount, double multiplier) => (int)Math.Min(Math.Floor(amount * multiplier), int.MaxValue);
}
