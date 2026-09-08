using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The one-step lookahead of the heuristic agents: what an action is expected to do, computed with the
/// domain's own resolution rules on snapshots, weighted between a critical and a non-critical roll by the
/// actor's critical chance, and scored with the weights (<c>docs/learning/agents.md</c>).
/// </summary>
public sealed class ActionScorer(IGameResources resources, RuleSet rules, ScoringWeights weights)
{
    /// <summary>How many rounds a permanent condition is worth in the score.</summary>
    public const int PermanentConditionRounds = 3;

    public ScoringWeights Weights => weights;

    /// <summary>The expected score of an action on a board: the crit and non-crit outcomes weighted by the crit chance.</summary>
    public double Expected(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var actor = creatures.First(creature => creature.Id == action.Actor);
        var chance = actor.CriticalChance.Plus(resources.GetSpell(action.Spell).Stats.CriticalChance.Value).Value;
        var critical = Score(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.Critical), creatures);
        var plain = Score(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical), creatures);
        return (chance * critical) + ((1 - chance) * plain);
    }

    /// <summary>Whether the action kills an enemy without a critical hit.</summary>
    public bool Kills(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical);
        return !resolution.Fizzled && Damage(resolution, creatures).Any(hit => hit.Kills && hit.Enemy);
    }

    /// <summary>The best target set of a spell for an actor, or null when the spell has no legal target.</summary>
    public (IReadOnlyList<CreatureId> Targets, double Score)? Best(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(spellId), creatures);
        (IReadOnlyList<CreatureId> Targets, double Score)? best = null;
        foreach (var targets in TargetSets.Of(legal))
        {
            var score = Expected(CombatAction.Bind(new CombatIntent(actor.Id, spellId), targets), creatures);
            if (best is null || score > best.Value.Score)
            {
                best = (targets, score);
            }
        }

        return best;
    }

    /// <summary>
    /// What a spell would be worth to an actor who knew it and could afford it, on the current board: the
    /// score of its best target set, or zero when it has none.
    /// </summary>
    public double Estimate(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var spell = resources.GetSpell(spellId);
        var hypothetical = actor with
        {
            KnownSpells = new HashSet<SpellId>(actor.KnownSpells) { spellId },
            Energy = Energy.Of(Math.Max(actor.Energy.Value, spell.Stats.Cost.Value)),
        };
        var board = creatures.Select(creature => creature.Id == actor.Id ? hypothetical : creature).ToList();
        return Best(hypothetical, spellId, board)?.Score ?? 0;
    }

    /// <summary>The score of one resolution: what it does to enemies counts for, what it does to allies against.</summary>
    public double Score(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(creatures);

        if (resolution.Fizzled)
        {
            return -weights.Risk;
        }

        var actor = creatures.First(creature => creature.Id == resolution.Action.Actor);
        var score = 0.0;
        foreach (var hit in Damage(resolution, creatures))
        {
            score += hit.Sign * ((weights.Damage * hit.Effective) + (hit.Kills ? weights.Kill : 0));
        }

        var remaining = RemainingHealth(resolution, creatures);
        foreach (var outcome in resolution.Outcomes)
        {
            score += outcome switch
            {
                HealOutcome heal => HealScore(actor, Target(heal.Target, creatures), heal.Amount),
                ConditionOutcome condition => ConditionScore(actor, Target(condition.Target, creatures), condition.Effect, remaining[condition.Target]),
                _ => 0,
            };
        }

        score += weights.Energy * (actor.Energy.Value - resolution.EnergySpent.Value);
        if (resolution.Action.Targets.Count > 0)
        {
            score -= weights.Risk * resolution.DroppedTargets.Count / resolution.Action.Targets.Count;
        }

        return score;
    }

    private static CreatureSnapshot Target(CreatureId id, IReadOnlyList<CreatureSnapshot> creatures) => creatures.First(creature => creature.Id == id);

    /// <summary>Plus one for something done to an enemy of the actor, minus one for something done to an ally or the actor.</summary>
    private static int Sign(CreatureSnapshot actor, CreatureSnapshot target) => target.Owner == actor.Owner ? -1 : 1;

    private static IEnumerable<(bool Enemy, int Sign, int Effective, bool Kills)> Damage(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var actor = Target(resolution.Action.Actor, creatures);
        foreach (var group in resolution.Outcomes.OfType<DamageOutcome>().GroupBy(outcome => outcome.Target))
        {
            var target = Target(group.Key, creatures);
            var total = group.Sum(outcome => outcome.Amount);
            var effective = Math.Min(total, target.Health.Value);
            yield return (target.Owner != actor.Owner, Sign(actor, target), effective, target.IsAlive && total >= target.Health.Value);
        }
    }

    private static Dictionary<CreatureId, int> RemainingHealth(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var remaining = creatures.ToDictionary(creature => creature.Id, creature => creature.Health.Value);
        foreach (var outcome in resolution.Outcomes.OfType<DamageOutcome>())
        {
            remaining[outcome.Target] = Math.Max(0, remaining[outcome.Target] - outcome.Amount);
        }

        return remaining;
    }

    /// <summary>Healing counts only what was missing; on an enemy it counts against.</summary>
    private double HealScore(CreatureSnapshot actor, CreatureSnapshot target, int amount) =>
        -Sign(actor, target) * weights.Heal * Math.Min(amount, target.MaxHealth.Value - target.Health.Value);

    private double ConditionScore(CreatureSnapshot actor, CreatureSnapshot target, LastingEffect effect, int remainingHealth)
    {
        if (remainingHealth == 0)
        {
            return 0;
        }

        var rounds = effect.Duration.Rounds ?? PermanentConditionRounds;
        var sign = Sign(actor, target);
        return effect switch
        {
            Stun => sign * weights.Stun,
            Bleed bleed => sign * weights.Bleed * Math.Min(bleed.AmountPerRound * rounds, remainingHealth),
            DefenseBuff buff => -sign * weights.Buff * buff.Amount * rounds,
            InitiativeDebuff debuff => sign * weights.Buff * debuff.Amount,
            _ => 0,
        };
    }
}
