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
/// <para>
/// Every reading comes in two forms: the <see cref="ScoreTerms"/>, one signed quantity per weight, and the
/// score, which is the weights applied to them. The terms are the single computation; a score is never
/// summed any other way, so what a dataset records per candidate (ADR 0051) is exactly what the heuristic
/// decided on.
/// </para>
/// </summary>
public sealed class ActionScorer(IGameResources resources, RuleSet rules, ScoringWeights weights)
{
    /// <summary>How many rounds a permanent condition is worth in the score.</summary>
    public const int PermanentConditionRounds = 3;

    public ScoringWeights Weights => weights;

    /// <summary>No creature is expected to be gone: what every reading outside a declaration assumes.</summary>
    public static readonly IReadOnlySet<CreatureId> NoneGone = new HashSet<CreatureId>();

    /// <summary>
    /// The expected score of an action on a board: the crit and non-crit outcomes weighted by the crit chance.
    /// <para>
    /// <paramref name="gone"/> names the creatures this action is expected to find dead by the time it
    /// resolves (ADR 0039). It is empty everywhere except a declaration, which is the one decision taken
    /// against a board that will have changed before the action lands.
    /// </para>
    /// </summary>
    public double Expected(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null) =>
        weights.Apply(ExpectedTerms(action, creatures, gone));

    /// <summary>The terms behind <see cref="Expected"/>: the crit and non-crit terms weighted by the crit chance.</summary>
    public ScoreTerms ExpectedTerms(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        gone ??= NoneGone;
        var actor = creatures.First(creature => creature.Id == action.Actor);
        var chance = actor.CriticalChance.Plus(resources.GetSpell(action.Spell).Stats.CriticalChance.Value).Value;
        var critical = Terms(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.Critical), creatures, gone);
        var plain = Terms(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical), creatures, gone);
        return (chance * critical) + ((1 - chance) * plain);
    }

    /// <summary>The actor's enemies this action is expected to kill on a plain roll, by id.</summary>
    public IEnumerable<CreatureId> Kills(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical);
        return resolution.Fizzled
            ? []
            : Damage(resolution, creatures, gone ?? NoneGone).Where(hit => hit.Kills && hit.Enemy).Select(hit => hit.Id);
    }

    /// <summary>Whether the action kills an enemy without a critical hit.</summary>
    public bool Kills(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical);
        return !resolution.Fizzled && Damage(resolution, creatures, NoneGone).Any(hit => hit.Kills && hit.Enemy);
    }

    /// <summary>The best target set of a spell for an actor, or null when the spell has no legal target.</summary>
    public (IReadOnlyList<CreatureId> Targets, double Score)? Best(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null) =>
        BestTerms(actor, spellId, creatures, gone) is { } best ? (best.Targets, weights.Apply(best.Terms)) : null;

    /// <summary>
    /// The best target set of a spell for an actor with the terms behind its score, or null when the spell has
    /// no legal target. Best under this scorer's weights: the terms say what that set does, the weights chose it.
    /// </summary>
    public (IReadOnlyList<CreatureId> Targets, ScoreTerms Terms)? BestTerms(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(spellId), creatures);
        (IReadOnlyList<CreatureId> Targets, ScoreTerms Terms)? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var targets in TargetSets.Of(legal))
        {
            var terms = ExpectedTerms(CombatAction.Bind(new CombatIntent(actor.Id, spellId), targets), creatures, gone);
            var score = weights.Apply(terms);
            if (best is null || score > bestScore)
            {
                best = (targets, terms);
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// What a spell would be worth to an actor who knew it and could afford it, on the current board: the
    /// score of its best target set, or zero when it has none.
    /// </summary>
    public double Estimate(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures) =>
        weights.Apply(EstimateTerms(actor, spellId, creatures));

    /// <summary>The terms behind <see cref="Estimate"/>: the best target set's, or none when the spell has no target.</summary>
    public ScoreTerms EstimateTerms(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
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
        return BestTerms(hypothetical, spellId, board)?.Terms ?? ScoreTerms.Zero;
    }

    /// <summary>
    /// What unlocking a spell is worth: what the spell would do in combat, plus the base initiative it buys for
    /// the rest of the match (ADR 0017), priced by the initiative weight (ADR 0018), minus the part of the cost
    /// the combat reading cannot see, priced by the energy weight (ADR 0020, ADR 0026).
    /// <para>
    /// Without the second term a pick taken for tempo scores as if it bought nothing. The third exists because
    /// <see cref="Estimate"/> raises the actor's energy to at least the spell's cost, so that a spell too
    /// expensive to cast today can still be read in combat. That raise is also what hides the cost: the score
    /// counts the energy the actor *keeps*, and a creature handed exactly what the spell costs keeps nothing
    /// whatever the spell costs. Below its cost the difference is invisible, so it is charged here; at or above
    /// it the keep term already prices every point, and charging again would price it twice.
    /// </para>
    /// </summary>
    public double UnlockValue(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures) =>
        weights.Apply(UnlockTerms(actor, spellId, creatures));

    /// <summary>The terms behind <see cref="UnlockValue"/>: the combat estimate, the initiative bought, the cost not covered.</summary>
    public ScoreTerms UnlockTerms(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var stats = resources.GetSpell(spellId).Stats;
        var combat = EstimateTerms(actor, spellId, creatures);
        return combat with
        {
            Initiative = combat.Initiative + stats.SpellInitiative.Value,
            Energy = combat.Energy - Math.Max(0, stats.Cost.Value - actor.Energy.Value),
        };
    }

    /// <summary>
    /// The score of one resolution: what it does to enemies counts for, what it does to allies against.
    /// <para>
    /// A creature in <paramref name="gone"/> is expected to be dead before this resolution lands, so nothing
    /// this action does to it counts and the action pays for the share of its targets that are in there
    /// (ADR 0039). The set is empty for every reading but a declaration.
    /// </para>
    /// </summary>
    public double Score(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null) =>
        weights.Apply(Terms(resolution, creatures, gone));

    /// <summary>The terms of one resolution, each signed: what it does to enemies counts for, to allies against.</summary>
    public ScoreTerms Terms(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(creatures);

        if (resolution.Fizzled)
        {
            return ScoreTerms.Zero;  // an action that came to nothing is worth nothing (ADR 0040)
        }

        gone ??= NoneGone;
        var actor = creatures.First(creature => creature.Id == resolution.Action.Actor);
        var terms = ScoreTerms.Zero;
        foreach (var hit in Damage(resolution, creatures, gone))
        {
            // Every point landed at the damage price, the last one at the kill price on top, and between the
            // two the share of the target's health the hit takes, at the pressure price: how much closer it
            // brings that creature to a kill (ADR 0050). Every term is signed the same way, so a hit an ally
            // takes counts against on all three.
            terms += new ScoreTerms(Damage: hit.Sign * hit.Effective, Kill: hit.Kills ? hit.Sign : 0, 0, 0, 0, 0, 0, 0, Pressure: hit.Sign * hit.Share);
        }

        var remaining = RemainingHealth(resolution, creatures, gone);
        foreach (var outcome in resolution.Outcomes)
        {
            terms += outcome switch
            {
                HealOutcome heal => HealTerms(actor, Target(heal.Target, creatures), heal.Amount, remaining[heal.Target]),
                EnergyOutcome energy => EnergyTerms(actor, Target(energy.Target, creatures), energy.Amount, remaining[energy.Target]),
                EnergyDrainOutcome drain => EnergyDrainTerms(actor, Target(drain.Target, creatures), drain.Amount, remaining[drain.Target]),
                ConditionOutcome condition => ConditionTerms(actor, Target(condition.Target, creatures), condition.Effect, remaining[condition.Target]),
                _ => ScoreTerms.Zero,
            };
        }

        terms += DefensiveTerms(actor, resolution, creatures, remaining);

        // What the actor keeps; what a spell hands out is priced per outcome above.
        return terms with { Energy = terms.Energy + (actor.Energy.Value - resolution.EnergySpent.Value) };
    }

    private static CreatureSnapshot Target(CreatureId id, IReadOnlyList<CreatureSnapshot> creatures) => creatures.First(creature => creature.Id == id);

    /// <summary>Plus one for something done to an enemy of the actor, minus one for something done to an ally or the actor.</summary>
    private static int Sign(CreatureSnapshot actor, CreatureSnapshot target) => target.Owner == actor.Owner ? -1 : 1;

    /// <summary>
    /// What a resolution's damage does to each creature it lands on: the points that count, whether they
    /// kill, and the share of the health the creature had that they take, one for a kill and nothing on a
    /// creature with no health left to take.
    /// </summary>
    private static IEnumerable<(CreatureId Id, bool Enemy, int Sign, int Effective, bool Kills, double Share)> Damage(
        CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId> gone)
    {
        var actor = Target(resolution.Action.Actor, creatures);
        foreach (var group in resolution.Outcomes.OfType<DamageOutcome>().GroupBy(outcome => outcome.Target))
        {
            if (gone.Contains(group.Key))
            {
                continue;  // a creature expected to be dead before this resolves takes none of it
            }

            var target = Target(group.Key, creatures);
            var total = group.Sum(outcome => outcome.Amount);
            var effective = Math.Min(total, target.Health.Value);
            var share = target.Health.Value > 0 ? (double)effective / target.Health.Value : 0.0;
            yield return (group.Key, target.Owner != actor.Owner, Sign(actor, target), effective, target.IsAlive && total >= target.Health.Value, share);
        }
    }

    /// <summary>
    /// The health each creature has left once this resolution has landed. A creature in <paramref name="gone"/>
    /// starts at zero, which is what silences every per-target term for it: healing, energy, drains,
    /// conditions and the defensive reading all already score nothing on a creature with no health left.
    /// </summary>
    private static Dictionary<CreatureId, int> RemainingHealth(
        CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId> gone)
    {
        var remaining = creatures.ToDictionary(
            creature => creature.Id,
            creature => gone.Contains(creature.Id) ? 0 : creature.Health.Value);
        foreach (var outcome in resolution.Outcomes.OfType<DamageOutcome>())
        {
            remaining[outcome.Target] = Math.Max(0, remaining[outcome.Target] - outcome.Amount);
        }

        return remaining;
    }

    /// <summary>
    /// Healing counts only what was missing; on an enemy it counts against. A target the same action kills is
    /// healed for nothing, so it scores nothing -- the rule the condition path already applies. What the
    /// healing saves from dying is priced once per target in <see cref="DefensiveTerms"/>, not here.
    /// </summary>
    private static ScoreTerms HealTerms(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth) =>
        remainingHealth == 0
            ? ScoreTerms.Zero
            : ScoreTerms.Zero with { Heal = -Sign(actor, target) * Math.Min(amount, target.MaxHealth.Value - target.Health.Value) };

    /// <summary>
    /// Energy given counts at the same price as energy kept, and counts against when it lands on an enemy.
    /// Energy has no cap, so unlike healing there is nothing to waste and nothing to clamp -- but a target the
    /// same action kills gains none of it, so it scores nothing either.
    /// </summary>
    private static ScoreTerms EnergyTerms(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth) =>
        remainingHealth == 0 ? ScoreTerms.Zero : ScoreTerms.Zero with { Energy = -Sign(actor, target) * amount };

    /// <summary>
    /// Energy taken is energy given with the sign turned over, clamped to what the target has because
    /// <c>Creature.LoseEnergy</c> takes no more than that (ADR 0035) -- the same reason <see cref="HealTerms"/>
    /// clamps to the health that is missing, and the same reason the damage path reads the health that is left:
    /// a resolution is priced before it is applied, so what the board can actually give up is read here.
    /// <para>
    /// The clamp is per outcome and not per target, so two drains in one cast would each be capped at the full
    /// pool and together price more than the target can lose. <see cref="HealTerms"/> has the same shape; only
    /// the damage path groups per target first. No spell carries two drains, and a spell that did would need
    /// the grouping rather than this note.
    /// </para>
    /// </summary>
    private static ScoreTerms EnergyDrainTerms(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth) =>
        -1 * EnergyTerms(actor, target, Math.Min(amount, target.Energy.Value), remainingHealth);

    private static ScoreTerms ConditionTerms(CreatureSnapshot actor, CreatureSnapshot target, LastingEffect effect, int remainingHealth)
    {
        if (remainingHealth == 0)
        {
            return ScoreTerms.Zero;
        }

        var rounds = effect.Duration.Rounds ?? PermanentConditionRounds;
        var sign = Sign(actor, target);
        return effect switch
        {
            Stun => ScoreTerms.Zero with { Stun = sign * rounds },
            Bleed bleed => ScoreTerms.Zero with { Bleed = sign * Math.Min(bleed.AmountPerRound * rounds, remainingHealth) },
            Regeneration regeneration => ScoreTerms.Zero with { Heal = -sign * Math.Min(regeneration.AmountPerRound * rounds, target.MaxHealth.Value - remainingHealth) },
            EnergyRegeneration energyRegeneration => ScoreTerms.Zero with { Energy = -sign * energyRegeneration.AmountPerRound * rounds },
            DefenseBuff => ScoreTerms.Zero,  // priced per target, with the rest of what the cast defends: see DefensiveTerms
            InitiativeBuff buff => ScoreTerms.Zero with { Initiative = -sign * buff.Amount * rounds },
            InitiativeDebuff debuff => ScoreTerms.Zero with { Initiative = sign * debuff.Amount * rounds },
            // A stand-in, and the same one `cast_value` uses for a buff: it does not read the damage the
            // debuff actually lets through, the way `DefensiveTerms` reads what a buff prevents (ADR 0035).
            // That costs the bot more than precision. `DefensiveTerms` is the only term that reads the threat
            // a creature faces, and only a DefenseBuff reaches it, so nothing here can see that lowering a
            // defense raises what the next hit takes -- neither an enemy's, which is the point of the debuff,
            // nor its own caster's, which is the cost of `psycho_rush`'s recoil. The price is right in shape
            // and the decision it feeds is blind; a spell that needs that seen needs `DefensiveTerms` to read
            // both kinds, which is a change to the one function every defensive spell is scored by.
            DefenseDebuff debuff => ScoreTerms.Zero with { Defense = sign * debuff.Amount * rounds },
            _ => ScoreTerms.Zero,
        };
    }

    /// <summary>
    /// What a cast is worth for what it defends, priced once per creature it touches (ADR 0022): the damage
    /// its defense buffs actually take off the threat over the rounds they last, plus the kill it denies when
    /// what it does turns a lethal round into a survivable one.
    /// <para>
    /// Both halves are read per target rather than per outcome because a cast can carry several defensive
    /// effects on one creature -- Guard carries two -- and neither half is additive over them. A point of
    /// defense past a hit's damage prevents nothing more of it, so buffs have to be priced on top of each
    /// other rather than each from the bare board; and a cast can deny one death per target, so scored per
    /// outcome the kill would be paid twice, or, when survival needs the effects together, not at all.
    /// </para>
    /// </summary>
    private ScoreTerms DefensiveTerms(CreatureSnapshot actor, CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, Dictionary<CreatureId, int> remaining)
    {
        var terms = ScoreTerms.Zero;
        foreach (var targetId in resolution.Outcomes
            .Where(outcome => outcome is HealOutcome or ConditionOutcome { Effect: DefenseBuff })
            .Select(outcome => outcome.Target)
            .Distinct())
        {
            var health = remaining[targetId];
            if (health == 0)
            {
                continue;
            }

            var target = Target(targetId, creatures);
            var allies = creatures.Count(creature => creature.Owner == target.Owner && creature.IsAlive);
            var bare = ThreatOn(target, creatures, extraDefense: 0);
            var sign = Sign(actor, target);
            var stacked = 0;
            var prevented = 0.0;

            // Longest first, so each buff is priced on top of the ones that outlast it.
            foreach (var buff in Buffs(resolution, targetId).OrderByDescending(buff => buff.Duration.Rounds ?? PermanentConditionRounds))
            {
                var before = ThreatOn(target, creatures, stacked);
                stacked += buff.Amount;
                prevented += (before - ThreatOn(target, creatures, stacked)) * (buff.Duration.Rounds ?? PermanentConditionRounds);
            }

            terms = terms with { Defense = terms.Defense - (sign * prevented / Math.Max(1, allies)) };
            if (bare >= health && ThreatOn(target, creatures, stacked) < health + Restored(resolution, target, targetId))
            {
                terms = terms with { Kill = terms.Kill - sign };
            }
        }

        return terms;
    }

    /// <summary>The defense buffs one resolution puts on one creature.</summary>
    private static IEnumerable<DefenseBuff> Buffs(CombatResolution resolution, CreatureId targetId) =>
        resolution.Outcomes.OfType<ConditionOutcome>()
            .Where(condition => condition.Target == targetId)
            .Select(condition => condition.Effect)
            .OfType<DefenseBuff>();

    /// <summary>The health one resolution's healing actually puts back on one creature, what it was missing being the cap.</summary>
    private static int Restored(CombatResolution resolution, CreatureSnapshot target, CreatureId targetId) =>
        Math.Min(
            resolution.Outcomes.OfType<HealOutcome>().Where(heal => heal.Target == targetId).Sum(heal => heal.Amount),
            target.MaxHealth.Value - target.Health.Value);

    /// <summary>
    /// The damage the living, unstunned enemies of a creature could deal it in one round: each contributes
    /// the best of the damaging spells it knows and can afford, weighted between its critical and its plain
    /// roll, after the defense the creature would have (ADR 0022). Read from the spells' own numbers rather
    /// than from a nested resolution, and computed only for an outcome that needs it, so scoring an attack
    /// costs exactly what it cost before.
    /// </summary>
    private double ThreatOn(CreatureSnapshot target, IReadOnlyList<CreatureSnapshot> creatures, int extraDefense)
    {
        var defense = target.TotalDefense.Value + extraDefense;
        var total = 0.0;
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
        }

        return total;
    }

    /// <summary>One hit as the resolution rules land it: the floored product, less the defense, never below zero.</summary>
    private static double Landed(int amount, double multiplier, int defense) =>
        Math.Max(0, Math.Floor(amount * multiplier) - defense);
}
