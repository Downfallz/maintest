using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;
using DamageEffect = DownfallArena.Domain.Resources.Effects.Damage;

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

    /// <summary>
    /// What unlocking a spell is worth: what the spell would do in combat, plus the base initiative it buys for
    /// the rest of the match (ADR 0017), priced by the initiative weight (ADR 0018). Without the second half a
    /// pick taken for tempo scores as if it bought nothing.
    /// </summary>
    public double UnlockValue(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        return Estimate(actor, spellId, creatures) + (weights.Initiative * resources.GetSpell(spellId).Stats.SpellInitiative.Value);
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
                HealOutcome heal => HealScore(actor, Target(heal.Target, creatures), heal.Amount, remaining[heal.Target], creatures),
                EnergyOutcome energy => EnergyScore(actor, Target(energy.Target, creatures), energy.Amount, remaining[energy.Target]),
                ConditionOutcome condition => ConditionScore(actor, Target(condition.Target, creatures), condition.Effect, remaining[condition.Target], creatures),
                _ => 0,
            };
        }

        score += weights.Energy * (actor.Energy.Value - resolution.EnergySpent.Value);  // what the actor keeps; what a spell hands out is priced per outcome above
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

    /// <summary>
    /// Healing counts only what was missing; on an enemy it counts against. A target the same action kills is
    /// healed for nothing, so it scores nothing -- the rule the condition path already applies. What the
    /// healing lifts out of reach of this round's threat is worth the kill it denies (ADR 0022).
    /// </summary>
    private double HealScore(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth, IReadOnlyList<CreatureSnapshot> creatures)
    {
        if (remainingHealth == 0)
        {
            return 0;
        }

        var restored = Math.Min(amount, target.MaxHealth.Value - target.Health.Value);
        var threat = ThreatOn(target, creatures, extraDefense: 0).Damage;
        var denied = threat >= remainingHealth && threat < remainingHealth + restored ? weights.Kill : 0;
        return -Sign(actor, target) * ((weights.Heal * restored) + denied);
    }

    /// <summary>
    /// Energy given counts at the same price as energy kept, and counts against when it lands on an enemy.
    /// Energy has no cap, so unlike healing there is nothing to waste and nothing to clamp -- but a target the
    /// same action kills gains none of it, so it scores nothing either.
    /// </summary>
    private double EnergyScore(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth) =>
        remainingHealth == 0 ? 0 : -Sign(actor, target) * weights.Energy * amount;

    private double ConditionScore(CreatureSnapshot actor, CreatureSnapshot target, LastingEffect effect, int remainingHealth, IReadOnlyList<CreatureSnapshot> creatures)
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
            Regeneration regeneration => -sign * weights.Heal * Math.Min(regeneration.AmountPerRound * rounds, target.MaxHealth.Value - remainingHealth),
            EnergyRegeneration energyRegeneration => -sign * weights.Energy * energyRegeneration.AmountPerRound * rounds,
            DefenseBuff buff => -sign * DefenseScore(target, creatures, buff.Amount, rounds, remainingHealth),
            InitiativeDebuff debuff => sign * weights.Initiative * debuff.Amount,
            _ => 0,
        };
    }

    /// <summary>
    /// What a defense buff is worth: the damage it takes off the hits the creature is expected to face while
    /// it lasts, plus the kill it denies when it turns a lethal round into a survivable one (ADR 0022).
    /// Defense subtracts from every incoming hit, so the value rises with the attackers a creature faces and
    /// falls with the allies they have to spread over.
    /// </summary>
    private double DefenseScore(CreatureSnapshot target, IReadOnlyList<CreatureSnapshot> creatures, int amount, int rounds, int remainingHealth)
    {
        var threat = ThreatOn(target, creatures, extraDefense: 0);
        if (threat.Attackers == 0)
        {
            return 0;
        }

        var allies = creatures.Count(creature => creature.Owner == target.Owner && creature.IsAlive);
        var prevented = amount * rounds * threat.Attackers / (double)Math.Max(1, allies);
        var denied = threat.Damage >= remainingHealth && ThreatOn(target, creatures, amount).Damage < remainingHealth
            ? weights.Kill
            : 0;
        return (weights.Buff * prevented) + denied;
    }

    /// <summary>
    /// The damage the living, unstunned enemies of a creature could deal it in one round: each contributes
    /// the best of the damaging spells it knows and can afford, weighted between its critical and its plain
    /// roll, after the defense the creature would have (ADR 0022). Read from the spells' own numbers rather
    /// than from a nested resolution, and computed only for an outcome that needs it, so scoring an attack
    /// costs exactly what it cost before.
    /// </summary>
    private Threat ThreatOn(CreatureSnapshot target, IReadOnlyList<CreatureSnapshot> creatures, int extraDefense)
    {
        var defense = target.TotalDefense.Value + extraDefense;
        var total = 0.0;
        var attackers = 0;
        foreach (var enemy in creatures)
        {
            if (enemy.Owner == target.Owner || enemy.IsDead || enemy.IsStunned)
            {
                continue;
            }

            var best = 0.0;
            foreach (var spellId in enemy.KnownSpells)
            {
                var spell = resources.GetSpell(spellId);
                if (spell.Stats.Cost.Value > enemy.Energy.Value)
                {
                    continue;
                }

                var chance = enemy.CriticalChance.Plus(spell.Stats.CriticalChance.Value).Value;
                var expected = spell.Effects.OfType<DamageEffect>()
                    .Sum(damage => (chance * Landed(damage.Amount, rules.CriticalMultiplier, defense)) + ((1 - chance) * Landed(damage.Amount, 1.0, defense)));
                best = Math.Max(best, expected);
            }

            total += best;
            if (best > 0)
            {
                attackers++;
            }
        }

        return new Threat(total, attackers);
    }

    /// <summary>One hit as the resolution rules land it: the floored product, less the defense, never below zero.</summary>
    private static double Landed(int amount, double multiplier, int defense) =>
        Math.Max(0, Math.Floor(amount * multiplier) - defense);

    /// <summary>What a creature faces in one round: the damage, and how many enemies can actually take some of it off.</summary>
    private readonly record struct Threat(double Damage, int Attackers);
}
