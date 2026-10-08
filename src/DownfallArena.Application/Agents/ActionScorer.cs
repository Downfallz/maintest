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
    /// <summary>
    /// The same scorer without <see cref="Unlocked"/>, which is what <see cref="Unlocked"/> prices an ally's
    /// spells with: Wait gives energy and every creature knows it, so reading the unlock with the unlock in it
    /// would never end. One level is the reading: what the energy lets the ally afford, not what that buys.
    /// </summary>
    private readonly Lazy<ActionScorer> _flat = new(() => new ActionScorer(resources, rules, weights, priceUnlocks: false));

    private readonly bool _priceUnlocks = true;

    /// <summary>The spells that stun, and the packages that teach one: read once, since the catalogue does not change.</summary>
    private readonly Lazy<(HashSet<SpellId> Spells, List<Tier> Tiers)> _stuns = new(() =>
    {
        var spells = resources.Spells.Where(spell => spell.Effects.Any(effect => effect is Stun)).Select(spell => spell.Id).ToHashSet();
        return (spells, [.. resources.Tiers.Where(tier => tier.Spells.Any(spells.Contains))]);
    });

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<CreatureSnapshot, List<(CreatureSnapshot[] Board, List<(SpellId, ScoreTerms, int, double, int)> Spells)>> _spells = [];

    /// <summary>How many boards a creature's spells are kept for: a decision reads one, a rollout's slot a few.</summary>
    private const int BoardsKept = 4;

    private ActionScorer(IGameResources resources, RuleSet rules, ScoringWeights weights, bool priceUnlocks)
        : this(resources, rules, weights)
    {
        _priceUnlocks = priceUnlocks;
    }

    /// <summary>
    /// This scorer without the unlock terms (ADR 0096), for a reading that plays the next round out and so
    /// sees what saved energy buys there, or that only asks which targets a spell would kill.
    /// </summary>
    public ActionScorer WithoutUnlocks => _priceUnlocks ? _flat.Value : this;

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
    /// <para>
    /// <paramref name="stillToAct"/> names the creatures whose slot comes after this action's in the round, read
    /// off the timeline: only an enemy among them can still land, this round, the hit a heal or a defense buff
    /// is cast against, so only they make the round lethal for the kill it denies (ADR 0085). <c>null</c> when
    /// no timeline is read, and then every living, unstunned enemy counts.
    /// </para>
    /// </summary>
    public double Expected(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, Speed speed = Speed.Standard, IReadOnlySet<CreatureId>? stillToAct = null) =>
        weights.Apply(ExpectedTerms(action, creatures, gone, speed, stillToAct));

    /// <summary>
    /// The terms behind <see cref="Expected"/>: the crit and non-crit terms weighted by the crit chance.
    /// <para>
    /// <paramref name="speed"/> decides whether there is a crit chance at all, since a Quick cast never crits.
    /// It defaults to <see cref="Speed.Standard"/>, which is an <em>assumption</em> and not a reading: a caller
    /// that knows the actor's speed should say so, and one that does not is pricing a critical the actor may
    /// not be able to roll. The rule itself takes no default — <see cref="ResolutionRules.Resolve"/> demands
    /// the speed — because mispricing costs an agent a good move while misresolving would change the game.
    /// </para>
    /// </summary>
    public ScoreTerms ExpectedTerms(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, Speed speed = Speed.Standard, IReadOnlySet<CreatureId>? stillToAct = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        gone ??= NoneGone;
        var actor = creatures.First(creature => creature.Id == action.Actor);
        var chance = ResolutionRules.CriticalChanceOf(actor, resources.GetSpell(action.Spell), speed);
        var plain = Terms(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical, speed), creatures, gone, stillToAct);

        // With no chance of a critical the mix below is the plain reading exactly -- nought times a finite
        // reading adds nothing -- so the second resolution is skipped rather than weighed at zero. A Quick cast
        // never crits, so this is most of the actions a turn scores.
        if (chance == 0)
        {
            return plain;
        }

        var critical = Terms(ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.Critical, speed), creatures, gone, stillToAct);
        return (chance * critical) + ((1 - chance) * plain);
    }

    /// <summary>The actor's enemies this action is expected to kill on a plain roll, by id.</summary>
    public IEnumerable<CreatureId> Kills(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        // The roll is forced plain, so the speed cannot change this reading: a Quick cast and a Standard one
        // both land their base damage here. Standard is passed because something must be, not as a claim.
        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical, Speed.Standard);
        return resolution.Fizzled
            ? []
            : Damage(resolution, creatures, gone ?? NoneGone).Where(hit => hit.Kills && hit.Enemy).Select(hit => hit.Id);
    }

    /// <summary>Whether the action kills an enemy without a critical hit.</summary>
    public bool Kills(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical, Speed.Standard);
        return !resolution.Fizzled && Damage(resolution, creatures, NoneGone).Any(hit => hit.Kills && hit.Enemy);
    }

    /// <summary>
    /// The target set an agent casts a spell on, or null when the spell has no legal target: one that wins the
    /// match on its own if there is one, then the best score (ADR 0099). <c>Wins</c> says whether it does. A win is
    /// never weighed against a score -- a weights file only has to be finite, so no score could be trusted to stay
    /// below it -- which is the order the lookahead's round already reads (ADR 0047).
    /// </summary>
    public (IReadOnlyList<CreatureId> Targets, double Score, bool Wins)? Best(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, Speed speed = Speed.Standard, IReadOnlySet<CreatureId>? stillToAct = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(spellId), creatures);
        (IReadOnlyList<CreatureId> Targets, double Score, bool Wins)? best = null;
        foreach (var targets in TargetSets.Of(legal))
        {
            var action = CombatAction.Bind(new CombatIntent(actor.Id, spellId), targets);
            var wins = Wins(action, creatures);
            var score = Expected(action, creatures, gone, speed, stillToAct);
            if (best is not { } found || (wins, score).CompareTo((found.Wins, found.Score)) > 0)
            {
                best = (targets, score, wins);
            }
        }

        return best;
    }

    /// <summary>
    /// Whether this action alone ends the match on a plain roll: whether, resolved on this board, it leaves none of
    /// the actor's enemies standing (ADR 0099). A win only a critical would bring is a chance, and the score prices
    /// it as one, through the kills it weighs by the critical chance; a win the plain roll brings is certain, and
    /// nothing outranks it. Every enemy counts, the ones the actor's own team is expected to kill first included:
    /// a kill that ends the match is never a wasted round, since if the ally's lands the round after it never
    /// comes, and if it does not this one is the win. A cast that also takes the last of the actor's own team
    /// down, through what it does to its caster, ends the match in a draw, and is no win.
    /// </summary>
    public bool Wins(CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);

        var actor = creatures.First(creature => creature.Id == action.Actor);
        var standing = creatures.Where(creature => creature.Owner != actor.Owner && creature.IsAlive).Select(creature => creature.Id).ToList();
        // Most of a match has more enemies standing than one cast reaches, and then nothing needs resolving. The
        // roll is forced plain, so the speed cannot change this reading, as in Kills.
        if (standing.Count == 0 || !standing.TrueForAll(action.Targets.Contains))
        {
            return false;
        }

        var resolution = ResolutionRules.Resolve(action, creatures, resources, rules, ForcedRandom.NotCritical, Speed.Standard);
        if (resolution.Fizzled)
        {
            return false;
        }

        var killed = Damage(resolution, creatures, NoneGone).Where(hit => hit.Kills).Select(hit => hit.Id).ToHashSet();
        return standing.TrueForAll(killed.Contains)
            && creatures.Any(creature => creature.Owner == actor.Owner && creature.IsAlive && !killed.Contains(creature.Id));
    }

    /// <summary>
    /// The best target set of a spell for an actor with the terms behind its score, or null when the spell has
    /// no legal target. Best under this scorer's weights: the terms say what that set does, the weights chose it.
    /// </summary>
    public (IReadOnlyList<CreatureId> Targets, ScoreTerms Terms)? BestTerms(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, Speed speed = Speed.Standard, IReadOnlySet<CreatureId>? stillToAct = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(spellId);
        ArgumentNullException.ThrowIfNull(creatures);

        var legal = TargetingRules.LegalTargets(actor, resources.GetSpell(spellId), creatures);
        (IReadOnlyList<CreatureId> Targets, ScoreTerms Terms)? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var targets in TargetSets.Of(legal))
        {
            var terms = ExpectedTerms(CombatAction.Bind(new CombatIntent(actor.Id, spellId), targets), creatures, gone, speed, stillToAct);
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
        // Read with the unlock terms (ADR 0096): what the buyer's purse reaches next round is how a dear package
        // is told from a cheap one. Read without them, Greedy kept buying cheap packages it never saves for, and
        // the terms bought it nothing. A spell that gives energy is read whole, since what it unlocks depends on
        // whom it reaches; any other is read without the terms and given its purse here, from the spells the
        // buyer already knows as they read on this board, which every package of a decision shares.
        if (!_priceUnlocks || spell.Effects.Any(effect => effect is EnergyGain))
        {
            return BestTerms(hypothetical, spellId, board)?.Terms ?? ScoreTerms.Zero;
        }

        if (_flat.Value.BestTerms(hypothetical, spellId, board) is not { } found)
        {
            return ScoreTerms.Zero;
        }

        var upkeep = Upkeep(actor);
        var purse = hypothetical.Energy.Value - spell.Stats.Cost.Value + upkeep;
        if (!Unlocks(hypothetical, upkeep, purse))
        {
            return found.Terms;
        }

        var bought = found.Terms with { Energy = 0 };
        var known = Spells(actor, creatures);
        var spells = actor.KnownSpells.Contains(spellId)
            ? known
            : [.. known, (spellId, bought, spell.Stats.Cost.Value, weights.Apply(bought), 0)];
        var ordered = spells.OrderBy(entry => entry.Id.Value, StringComparer.Ordinal).ToList();
        return found.Terms + Gained(Affordable(ordered, purse).Terms, Affordable(ordered, upkeep).Terms);
    }

    /// <summary>
    /// What buying a package is worth: the best of its spells in combat, plus the turn order the package's
    /// initiative bonus buys for the rest of the match (ADR 0056, read as the enemies it moves the buyer past
    /// since ADR 0088), priced by the initiative weight (ADR 0018), minus the part of that spell's cost the
    /// combat reading cannot see, priced by the energy weight (ADR 0020, ADR 0026).
    /// <para>
    /// Without the second term a pick taken for tempo scores as if it bought nothing. The third exists because
    /// <see cref="Estimate"/> raises the actor's energy to at least the spell's cost, so that a spell too
    /// expensive to cast today can still be read in combat. That raise is also what hides the cost: the score
    /// counts the energy the actor *keeps*, and a creature handed exactly what the spell costs keeps nothing
    /// whatever the spell costs. Below its cost the difference is invisible, so it is charged here; at or above
    /// it the keep term already prices every point, and charging again would price it twice.
    /// </para>
    /// <para>
    /// The best of the package's spells rather than the sum of them, because a creature casts one spell a
    /// round: summing would price a three-spell package as three simultaneous casts and make the deepest
    /// packages look three times as good as they play. It undervalues the option a second spell is, which no
    /// one-step reading can price, and that is the error this takes. Every weight here was fitted against one
    /// spell per pick, so this pricing is provisional until they are refitted (ADR 0056).
    /// </para>
    /// <para>
    /// A spell is worth what it adds over the best spell of its kind -- offensive, defensive or passive -- the
    /// creature already knows, both read alike on this board, unlocks included, and nothing when it adds nothing
    /// (ADR 0102). A creature casts one spell a round, so a second spell that does what a known one does better
    /// is a card it never plays, and pricing it at its whole cast had bots buy them late in every match. The
    /// comparison is within a kind because a guard is not the hit it stands beside: it is cast on the rounds a
    /// hit is not. What a spell does is compared; the energy term is not, since it is the purse the purchase
    /// leaves and what it costs. A spell that adds something, or that moves energy at all, carries its own.
    /// </para>
    /// <para>
    /// A spell the creature already knows is not part of what the package sells: two packages may teach the
    /// same spell, and <see cref="Domain.Matches.Creatures.Creature.BuyTier"/> grants it idempotently, so
    /// pricing it again would have an agent pay a pick for a combat option it already has. A package whose
    /// every spell is already known is still worth the order its initiative bonus buys, and nothing else.
    /// </para>
    /// </summary>
    public double PurchaseValue(CreatureSnapshot actor, TierId tierId, IReadOnlyList<CreatureSnapshot> creatures) =>
        weights.Apply(PurchaseTerms(actor, tierId, creatures));

    /// <summary>The terms behind <see cref="PurchaseValue"/>: what the best spell adds over its kind, the initiative bought, the cost not covered, and the passive.</summary>
    public ScoreTerms PurchaseTerms(CreatureSnapshot actor, TierId tierId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(tierId);
        ArgumentNullException.ThrowIfNull(creatures);

        var tier = resources.GetTier(tierId);
        var known = new Dictionary<SpellType, ScoreTerms>();
        var best = ScoreTerms.Zero;
        var bestScore = double.NegativeInfinity;
        foreach (var spellId in tier.Spells.Where(spell => !actor.KnownSpells.Contains(spell)).OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            var kind = resources.GetSpell(spellId).Type;
            if (!known.TryGetValue(kind, out var beaten))
            {
                beaten = BestOfKind(actor, kind, creatures);
                known[kind] = beaten;
            }

            var spell = resources.GetSpell(spellId);
            var purchase = AsPurchase(actor, spellId, creatures);
            var gained = Gained(purchase, beaten);
            if (weights.Apply(gained) <= 0 && !spell.Effects.Any(effect => effect is EnergyGain or EnergyRegeneration or EnergyDrain))
            {
                continue;
            }

            var terms = gained + (ScoreTerms.Zero with { Energy = purchase.Energy });
            var score = weights.Apply(terms);
            if (score > 0 && score > bestScore)
            {
                best = terms;
                bestScore = score;
            }
        }

        return PassiveTerms(actor, tier.Passive, creatures) + (best with { Initiative = best.Initiative + Overtaken(actor, tier.InitiativeBonus.Value, creatures) });
    }

    /// <summary>
    /// One spell read as a purchase: its estimate on the board, unlocks included (ADR 0096), less the part of
    /// its cost the creature cannot cover (ADR 0026).
    /// </summary>
    private ScoreTerms AsPurchase(CreatureSnapshot actor, SpellId spellId, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var combat = EstimateTerms(actor, spellId, creatures);
        return combat with { Energy = combat.Energy - Math.Max(0, resources.GetSpell(spellId).Stats.Cost.Value - actor.Energy.Value) };
    }

    /// <summary>
    /// The best spell of <paramref name="kind"/> the creature already knows, read the way a purchase is (ADR
    /// 0102): what a new spell of that kind has to beat to be cast instead. Both sides are read alike, unlocks
    /// and energy included, so a difference between them is the spell's and not the reading's. Nothing when it
    /// knows none.
    /// </summary>
    private ScoreTerms BestOfKind(CreatureSnapshot actor, SpellType kind, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var best = ScoreTerms.Zero;
        var bestScore = 0.0;
        foreach (var spellId in actor.KnownSpells.Where(spell => resources.GetSpell(spell).Type == kind).OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            var terms = AsPurchase(actor, spellId, creatures);
            var score = weights.Apply(terms);
            if (score > bestScore)
            {
                best = terms;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// What a package's passive is worth to its buyer (ADR 0101), over the rounds a permanent condition is read
    /// for, each priced by the casts it changes, so that it weighs against a spell package's one cast:
    /// <list type="bullet">
    /// <item>energy at upkeep as the energy itself and the better spell it pays for every round, the way an
    /// energy gift is priced by the spell it unlocks (ADR 0096);</item>
    /// <item>a damage bonus as the best cast it makes, every hit of it raised, against the best cast without it,
    /// one cast a round;</item>
    /// <item>stun immunity as the buyer's best cast kept, once for each living enemy that knows a stun and half
    /// for one that could buy one at its next pick, up to one a round: a stun costs its caster a cast, so no
    /// enemy is read landing one every round.</item>
    /// </list>
    /// </summary>
    private ScoreTerms PassiveTerms(CreatureSnapshot actor, Passive passive, IReadOnlyList<CreatureSnapshot> creatures)
    {
        if (!passive.GivesAnything())
        {
            return ScoreTerms.Zero;
        }

        var kit = Spells(actor, creatures);
        var terms = ScoreTerms.Zero;
        if (passive.UpkeepEnergy > 0)
        {
            var upkeep = Upkeep(actor);
            var unlocked = Gained(Sustained(kit, upkeep + passive.UpkeepEnergy), Sustained(kit, upkeep));
            terms += (PermanentConditionRounds * unlocked) with { Energy = passive.UpkeepEnergy * PermanentConditionRounds };
        }

        if (passive.DamageBonus > 0)
        {
            terms += PermanentConditionRounds * Raised(actor, kit, creatures, passive.DamageBonus);
        }

        if (passive.StunImmunity && !actor.Passive.StunImmunity)
        {
            var stunners = creatures.Where(creature => creature.Owner != actor.Owner && creature.IsAlive).Sum(StunThreat);
            terms += Math.Min(stunners, PermanentConditionRounds) * Affordable(kit, Upkeep(actor)).Terms;
        }

        return terms;
    }

    /// <summary>
    /// How much of a stunner an enemy is: one that knows a stun is one, one that could buy a package teaching
    /// one at its next pick is half, since it may buy something else, and any other is none.
    /// </summary>
    private double StunThreat(CreatureSnapshot enemy)
    {
        if (enemy.KnownSpells.Any(_stuns.Value.Spells.Contains))
        {
            return 1;
        }

        return _stuns.Value.Tiers.Any(tier => !enemy.AcquiredTiers.Contains(tier.Id) && tier.IsOpenTo(enemy.AcquiredTiers.Contains)) ? 0.5 : 0;
    }

    /// <summary>
    /// What a creature casts a round, on average, when every round gives it <paramref name="upkeep"/> energy and
    /// it saves for what it casts: a spell dearer than a round's energy is cast that share of the rounds, and
    /// the creature casts whichever spell is worth the most read so.
    /// </summary>
    private static ScoreTerms Sustained(List<(SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)> kit, int upkeep)
    {
        var best = ScoreTerms.Zero;
        var bestScore = 0.0;
        foreach (var (_, terms, cost, score, _) in kit)
        {
            var share = cost <= upkeep ? 1.0 : (double)upkeep / cost;
            if (share * score > bestScore)
            {
                best = share * terms;
                bestScore = share * score;
            }
        }

        return best;
    }

    /// <summary>
    /// What a damage bonus adds to one round's cast: the best of the creature's spells once each raised by the
    /// bonus on every hit it lands -- each damage effect once on each enemy it can reach, a critical one counted
    /// at the multiplier, since the bonus is added before it -- against the best of them as they are. A spell
    /// that hits several enemies gains the bonus on each, and may become the cast worth making.
    /// </summary>
    private ScoreTerms Raised(CreatureSnapshot actor, List<(SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)> kit, IReadOnlyList<CreatureSnapshot> creatures, int bonus)
    {
        var enemies = creatures.Count(creature => creature.Owner != actor.Owner && creature.IsAlive);
        var now = ScoreTerms.Zero;
        var nowScore = 0.0;
        var raised = ScoreTerms.Zero;
        var raisedScore = 0.0;
        foreach (var (id, spellTerms, _, score, _) in kit)
        {
            if (score > nowScore)
            {
                now = spellTerms;
                nowScore = score;
            }

            var spell = resources.GetSpell(id);
            var damages = spell.Effects.Count(effect => effect is DamageEffect);
            var reached = Reached(spell.Targeting, enemies);
            var chance = actor.CriticalChance.Plus(spell.Stats.CriticalChance.Value).Value;
            var gained = spellTerms + (ScoreTerms.Zero with { Damage = bonus * damages * reached * (1 + (chance * (rules.CriticalMultiplier - 1))) });
            if (weights.Apply(gained) > raisedScore)
            {
                raised = gained;
                raisedScore = weights.Apply(gained);
            }
        }

        return raised + (-1 * now);
    }

    /// <summary>How many enemies one cast of a spell targeting so reaches, out of <paramref name="enemies"/> living.</summary>
    private static int Reached(TargetingSpec targeting, int enemies)
    {
        if (targeting.Origin != TargetOrigin.Enemy)
        {
            return 0;
        }

        return targeting.Scope == TargetScope.SingleTarget ? 1 : Math.Min(targeting.MaxTargets ?? enemies, enemies);
    }

    /// <summary>
    /// The score of one resolution: what it does to enemies counts for, what it does to allies against.
    /// <para>
    /// A creature in <paramref name="gone"/> is expected to be dead before this resolution lands, so nothing
    /// this action does to it counts and the action pays for the share of its targets that are in there
    /// (ADR 0039). The set is empty for every reading but a declaration.
    /// </para>
    /// </summary>
    public double Score(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, IReadOnlySet<CreatureId>? stillToAct = null) =>
        weights.Apply(Terms(resolution, creatures, gone, stillToAct));

    /// <summary>
    /// The terms of one resolution, each signed: what it does to enemies counts for, to allies against.
    /// </summary>
    public ScoreTerms Terms(CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? gone = null, IReadOnlySet<CreatureId>? stillToAct = null)
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
            terms += ScoreTerms.Zero with { Damage = hit.Sign * hit.Effective, Kill = hit.Kills ? hit.Sign : 0, Pressure = hit.Sign * hit.Share };
        }

        var remaining = RemainingHealth(resolution, creatures, gone);
        foreach (var outcome in resolution.Outcomes)
        {
            terms += outcome switch
            {
                HealOutcome heal => HealTerms(actor, Target(heal.Target, creatures), heal.Amount, remaining[heal.Target]),
                EnergyOutcome energy => EnergyTerms(actor, Target(energy.Target, creatures), energy.Amount, remaining[energy.Target])
                    + Unlocked(actor, Target(energy.Target, creatures), energy.Amount, remaining[energy.Target], creatures, stillToAct),
                EnergyDrainOutcome drain => EnergyDrainTerms(actor, Target(drain.Target, creatures), drain.Amount, remaining[drain.Target], stillToAct),
                ConditionOutcome condition => ConditionTerms(actor, Target(condition.Target, creatures), condition.Effect, remaining, creatures),
                _ => ScoreTerms.Zero,
            };
        }

        terms += DefensiveTerms(actor, resolution, creatures, remaining, stillToAct);
        terms += NextPurse(actor, resolution, creatures, remaining[actor.Id]);

        // What the actor keeps; what a spell hands out is priced per outcome above. The actor's own purse,
        // what it keeps and what the spell gives it back, is worth only up to the reserve its dearest spell
        // can spend (ADR 0103): energy hoarded past that pays for nothing.
        // A gain that lands on a caster its own cast killed is refused, and priced at nothing above.
        var given = remaining[actor.Id] == 0 ? 0 : resolution.Outcomes.OfType<EnergyOutcome>().Where(energy => energy.Target == actor.Id).Sum(energy => energy.Amount);
        var purse = actor.Energy.Value - resolution.EnergySpent.Value + given;
        return terms with { Energy = terms.Energy - given + Math.Min(purse, Reserve(actor)) };
    }

    private static CreatureSnapshot Target(CreatureId id, IReadOnlyList<CreatureSnapshot> creatures) => creatures.First(creature => creature.Id == id);

    /// <summary>Plus one for something done to an enemy of the actor, minus one for something done to an ally or the actor.</summary>
    private static int Sign(CreatureSnapshot actor, CreatureSnapshot target) => target.Owner == actor.Owner ? -1 : 1;

    /// <summary>
    /// How far a change of <paramref name="delta"/> initiative moves <paramref name="target"/> through the turn
    /// order, counted in the living creatures of the other team it gets ahead of (positive) or falls behind
    /// (negative), with a tie counting half because a d20 decides it (ADR 0063). This is what initiative buys
    /// (ADR 0088): the timeline orders on it, and a point that passes nobody changes no slot. Initiative is
    /// floored at zero (ADR 0036), so a debuff is read only as far down as that.
    /// <para>
    /// It reads the board as it stands, the way every other term does: the order a buff changes is the order
    /// of the next timeline, which the speeds still to be chosen can rearrange (a <c>Quick</c> creature acts
    /// before every <c>Standard</c> one) and nothing here can see. An enemy the same resolution kills, which
    /// <paramref name="remaining"/> says when there is a resolution, holds no slot while the change lasts.
    /// </para>
    /// <para>
    /// The change is added to the total before the floor, as the creature adds it (ADR 0036): a creature held
    /// at zero by debuffs deeper than its initiative has to climb out of that deficit before it moves.
    /// </para>
    /// </summary>
    private static double Overtaken(
        CreatureSnapshot target, int delta, IReadOnlyList<CreatureSnapshot> creatures, Dictionary<CreatureId, int>? remaining = null)
    {
        var before = target.CurrentInitiative.Value;
        var after = Math.Max(0, UnflooredInitiative(target) + delta);
        return creatures
            .Where(other => other.Owner != target.Owner && other.IsAlive && (remaining is null || remaining[other.Id] > 0))
            .Sum(other => Ahead(after, other.CurrentInitiative.Value) - Ahead(before, other.CurrentInitiative.Value));
    }

    /// <summary>
    /// The initiative a creature would have without the floor at zero. Above zero the floor did not bite, so
    /// it is the current initiative; at zero it is the base plus the buffs less the debuffs, which may be below.
    /// </summary>
    private static int UnflooredInitiative(CreatureSnapshot creature)
    {
        if (creature.CurrentInitiative.Value > 0)
        {
            return creature.CurrentInitiative.Value;
        }

        var buffs = creature.Conditions.Select(condition => condition.Effect).OfType<InitiativeBuff>().Sum(buff => buff.Amount);
        var debuffs = creature.Conditions.Select(condition => condition.Effect).OfType<InitiativeDebuff>().Sum(debuff => debuff.Amount);
        return Math.Min(0, creature.BaseInitiative.Value + buffs - debuffs);
    }

    /// <summary>One for acting first, none for acting after, a half for a tie the dice decide.</summary>
    private static double Ahead(int mine, int theirs) => (Math.Sign(mine - theirs) + 1) / 2.0;

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
    /// What the actor's own energy buys next round (ADR 0096): the best spell its purse will pay for then, read
    /// on this board, less the best one the round's gain alone pays for. A creature that spends two a round on
    /// two-energy spells never reaches a spell that costs four; one that waits a round, or spends on a cheaper
    /// spell, does. Read for every action, so the choice between a two-energy spell now and a four-energy one
    /// next round is a choice between the two rounds and not between this round's hit and nothing.
    /// <para>
    /// The purse is the energy the actor has, less what this action costs, plus what it gives itself, plus a
    /// round's gain. The baseline is what a creature that spent everything would pay for, which every action
    /// shares, so it moves no choice and keeps the term at zero for an action that leaves nothing over.
    /// </para>
    /// </summary>
    private ScoreTerms NextPurse(CreatureSnapshot actor, CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, int remainingHealth)
    {
        if (!_priceUnlocks || remainingHealth == 0)
        {
            return ScoreTerms.Zero;
        }

        var given = resolution.Outcomes.OfType<EnergyOutcome>().Where(outcome => outcome.Target == actor.Id).Sum(outcome => outcome.Amount);
        var upkeep = Upkeep(actor);
        var purse = actor.Energy.Value - resolution.EnergySpent.Value + given + upkeep;
        return Unlocks(actor, upkeep, purse)
            ? Gained(Affordable(actor, purse, creatures).Terms, Affordable(actor, upkeep, creatures).Terms)
            : ScoreTerms.Zero;
    }

    /// <summary>
    /// What energy given to another creature buys beyond its price per point: the spell it lets that creature
    /// afford next round and could not have without it (ADR 0096). The creature is taken to cast this round the
    /// best spell it can pay for now when it is still to act, or when the round's order is not read, and to wait
    /// when nothing it can pay for is worth anything (<see cref="Spent"/>); one that has acted, or is stunned,
    /// spends nothing more this round. Energy given this round cannot pay for a
    /// spell this round, since every intent is declared before anything resolves. Signed like every other
    /// term: on an enemy it counts against. What the actor gives itself is read by <see cref="NextPurse"/>,
    /// not here. Each energy outcome is read alone, so a spell giving one creature energy twice would be
    /// credited the unlock twice; no spell does.
    /// </summary>
    private ScoreTerms Unlocked(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth, IReadOnlyList<CreatureSnapshot> creatures, IReadOnlySet<CreatureId>? stillToAct)
    {
        if (!_priceUnlocks || amount <= 0 || remainingHealth == 0 || target.Id == actor.Id)
        {
            return ScoreTerms.Zero;
        }

        var spends = !target.IsStunned && (stillToAct is null || stillToAct.Contains(target.Id));
        var upkeep = Upkeep(target);
        if (!Unlocks(target, upkeep, target.Energy.Value + upkeep + amount + (spends ? MostGained(target) : 0)))
        {
            return ScoreTerms.Zero;
        }

        var purse = target.Energy.Value - (spends ? Spent(target, creatures) : 0) + upkeep;
        return Unlocks(target, purse, purse + amount)
            ? -Sign(actor, target) * Gained(Affordable(target, purse + amount, creatures).Terms, Affordable(target, purse, creatures).Terms)
            : ScoreTerms.Zero;
    }

    /// <summary>
    /// What a creature still to act takes out of its energy this round, net of what its spell gives it back: the
    /// best spell it can pay for, or, when none is worth anything, the free one that gives it the most energy, as
    /// Wait does. A creature with nothing worth casting waits; it does not stand still.
    /// </summary>
    private int Spent(CreatureSnapshot creature, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var affordable = Spells(creature, creatures).Where(spell => spell.Cost <= creature.Energy.Value).ToList();
        (SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)? best = null;
        foreach (var spell in affordable.Where(spell => spell.Score > 0 && (best is null || spell.Score > best.Value.Score)))
        {
            best = spell;
        }

        return best is { } cast ? cast.Cost - cast.Gain : -affordable.Select(spell => spell.Gain - spell.Cost).DefaultIfEmpty(0).Max();
    }

    /// <summary>The most energy any spell the creature can pay for now could give it back, read without a board.</summary>
    private int MostGained(CreatureSnapshot creature) =>
        creature.KnownSpells
            .Select(resources.GetSpell)
            .Where(spell => spell.Stats.Cost.Value <= creature.Energy.Value && spell.Targeting.Origin is TargetOrigin.Self or TargetOrigin.Ally)
            .Select(spell => spell.Effects.OfType<EnergyGain>().Sum(effect => effect.Amount))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>The energy a creature gains at the next upkeep: the rule set's, and its packages' passive (ADR 0101).</summary>
    private int Upkeep(CreatureSnapshot creature) => rules.EnergyPerRound + creature.Passive.UpkeepEnergy;

    /// <summary>
    /// The most energy worth holding (ADR 0103): what it takes to cast the creature's dearest spell every round
    /// for the rounds a permanent condition is read for. Its cost now, and for each round after, what a round's
    /// energy does not cover: Crushing Stomp at 4 on 2 a round wants 8, a spell no dearer than a round's energy
    /// wants its cost. A point past that pays for no cast, so it is worth nothing. The spells it knows, not the
    /// ones it may buy: a purchase raises the reserve the round it lands.
    /// </summary>
    private int Reserve(CreatureSnapshot creature)
    {
        var dearest = creature.KnownSpells.Select(spell => resources.GetSpell(spell).Stats.Cost.Value).DefaultIfEmpty(0).Max();
        return dearest + ((PermanentConditionRounds - 1) * Math.Max(0, dearest - Upkeep(creature)));
    }

    /// <summary>
    /// Whether a purse of <paramref name="richer"/> pays for a spell the creature knows that one of
    /// <paramref name="poorer"/> does not. When it does not, the best spell either pays for is the same one and
    /// the difference is nothing, which is most boards: a creature whose spells all cost what a round gives
    /// has nothing to save for. Read before anything is resolved, because the reading it spares is not cheap.
    /// </summary>
    private bool Unlocks(CreatureSnapshot creature, int poorer, int richer) =>
        richer > poorer && creature.KnownSpells.Any(spell => resources.GetSpell(spell).Stats.Cost.Value is var cost && cost > poorer && cost <= richer);

    /// <summary>
    /// The difference between two spells' terms, without the energy each would leave: the points given are
    /// priced once, per point, by <see cref="EnergyTerms"/> and the energy kept.
    /// </summary>
    private ScoreTerms Gained(ScoreTerms with, ScoreTerms without)
    {
        var gained = (with + (-1 * without)) with { Energy = 0 };
        return weights.Apply(gained) > 0 ? gained : ScoreTerms.Zero;
    }

    /// <summary>
    /// The best spell a creature knows and could pay for with <paramref name="purse"/>, read without unlocks,
    /// and what it costs.
    /// </summary>
    private (ScoreTerms Terms, int Cost) Affordable(CreatureSnapshot creature, int purse, IReadOnlyList<CreatureSnapshot> creatures) =>
        Affordable(Spells(creature, creatures), purse);

    private static (ScoreTerms Terms, int Cost) Affordable(IEnumerable<(SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)> spells, int purse)
    {
        var best = (Terms: ScoreTerms.Zero, Cost: 0);
        var bestScore = 0.0;
        foreach (var (_, terms, cost, score, _) in spells)
        {
            if (cost <= purse && score > bestScore)
            {
                best = (terms, cost);
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// Every spell a creature knows, each at its best target set on this board, read without unlocks and
    /// without the energy it would leave, in ordinal id order. The creature is funded for the dearest of them,
    /// since a spell it cannot pay for does not resolve; past that, nothing a spell resolves to reads the
    /// caster's energy, so a purse only filters this list. It is kept per creature snapshot and board, a board
    /// being the same snapshots in the same order: every action of a decision reads the same board, and a
    /// rollout's speed, intent and targets each build a new list of the same snapshots.
    /// </summary>
    private List<(SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)> Spells(CreatureSnapshot creature, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var kept = _spells.GetValue(creature, _ => []);
        lock (kept)
        {
            foreach (var (board, spells) in kept)
            {
                if (Same(board, creatures))
                {
                    return spells;
                }
            }
        }

        var read = ReadSpells(creature, creatures);
        lock (kept)
        {
            if (kept.Count == BoardsKept)
            {
                kept.RemoveAt(0);
            }

            kept.Add(([.. creatures], read));
        }

        return read;
    }

    private static bool Same(CreatureSnapshot[] board, IReadOnlyList<CreatureSnapshot> creatures)
    {
        if (board.Length != creatures.Count)
        {
            return false;
        }

        for (var index = 0; index < board.Length; index++)
        {
            if (!ReferenceEquals(board[index], creatures[index]))
            {
                return false;
            }
        }

        return true;
    }

    private List<(SpellId Id, ScoreTerms Terms, int Cost, double Score, int Gain)> ReadSpells(CreatureSnapshot creature, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var dearest = creature.KnownSpells.Select(spell => resources.GetSpell(spell).Stats.Cost.Value).DefaultIfEmpty(0).Max();
        var funded = creature with { Energy = Energy.Of(Math.Max(creature.Energy.Value, dearest)) };
        var board = creatures.Select(other => other.Id == creature.Id ? funded : other).ToList();
        List<(SpellId, ScoreTerms, int, double, int)> spells = [];
        foreach (var spell in creature.KnownSpells.OrderBy(spell => spell.Value, StringComparer.Ordinal))
        {
            if (_flat.Value.BestTerms(funded, spell, board) is { } found)
            {
                var terms = found.Terms with { Energy = 0 };
                var gain = found.Targets.Contains(creature.Id) ? resources.GetSpell(spell).Effects.OfType<EnergyGain>().Sum(effect => effect.Amount) : 0;
                spells.Add((spell, terms, resources.GetSpell(spell).Stats.Cost.Value, weights.Apply(terms), gain));
            }
        }

        return spells;
    }

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
    private ScoreTerms EnergyDrainTerms(CreatureSnapshot actor, CreatureSnapshot target, int amount, int remainingHealth, IReadOnlySet<CreatureId>? stillToAct)
    {
        var taken = Math.Min(amount, target.Energy.Value);
        var terms = -1 * EnergyTerms(actor, target, taken, remainingHealth);
        return Locks(actor, target, taken, remainingHealth, stillToAct) ? terms with { Stun = terms.Stun + 1 } : terms;
    }

    /// <summary>
    /// Whether a drain takes this round's action from an enemy, which a stun does too and is priced the same,
    /// for one round (ADR 0093). An enemy that has still to act and could pay for one of its spells before the
    /// drain, and can pay for none after it, reaches its slot unable to pay and fizzles: the rules read it as
    /// unable to act, the way they read a stun. Read per energy price, the same drain was worth a third of a
    /// point a point of energy, so the lock it buys every round was the one thing about it no agent saw.
    /// <para>
    /// It is priced as a stun and is not one: no immunity follows it, and a spell that costs nothing still
    /// resolves. The enemy's intent is hidden, so the reading is its cheapest spell that costs anything,
    /// which takes the lock away only when the enemy could not have paid for anything anyway. With no
    /// <paramref name="stillToAct"/> every enemy counts as still to act, the convention the threat reading
    /// keeps (ADR 0085).
    /// </para>
    /// </summary>
    private bool Locks(CreatureSnapshot actor, CreatureSnapshot target, int taken, int remainingHealth, IReadOnlySet<CreatureId>? stillToAct)
    {
        if (taken == 0 || remainingHealth == 0 || target.Owner == actor.Owner || target.IsStunned
            || (stillToAct is not null && !stillToAct.Contains(target.Id)))
        {
            return false;
        }

        var costs = target.KnownSpells.Select(spell => resources.GetSpell(spell).Stats.Cost.Value).Where(cost => cost > 0).ToList();
        if (costs.Count == 0)
        {
            return false;
        }

        var cheapest = costs.Min();
        return target.Energy.Value >= cheapest && target.Energy.Value - taken < cheapest;
    }

    private ScoreTerms ConditionTerms(
        CreatureSnapshot actor, CreatureSnapshot target, LastingEffect effect, Dictionary<CreatureId, int> remaining, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var remainingHealth = remaining[target.Id];
        if (remainingHealth == 0)
        {
            return ScoreTerms.Zero;
        }

        var rounds = effect.Duration.Rounds ?? PermanentConditionRounds;
        var sign = Sign(actor, target);
        return effect switch
        {
            Stun => ScoreTerms.Zero with { Stun = sign * rounds },
            Bleed bleed => BleedTerms(sign, Math.Min(bleed.AmountPerRound * rounds, remainingHealth), target.TotalDefense.Value * rounds),
            Regeneration regeneration => ScoreTerms.Zero with { Heal = -sign * Math.Min(regeneration.AmountPerRound * rounds, target.MaxHealth.Value - remainingHealth) },
            EnergyRegeneration energyRegeneration => ScoreTerms.Zero with { Energy = -sign * energyRegeneration.AmountPerRound * rounds },
            DefenseBuff => ScoreTerms.Zero,  // priced per target, with the rest of what the cast defends: see DefensiveTerms
            // A hit a round raised by the amount (ADR 0101): the holder's own direct hits, which it lands on its enemies.
            DamageBuff buff => ScoreTerms.Zero with { Damage = -sign * buff.Amount * rounds },
            InitiativeBuff buff => ScoreTerms.Zero with { Initiative = -sign * Overtaken(target, buff.Amount, creatures, remaining) * rounds },
            InitiativeDebuff debuff => ScoreTerms.Zero with { Initiative = -sign * Overtaken(target, -debuff.Amount, creatures, remaining) * rounds },
            DefenseDebuff debuff => DefenseDebuffTerms(sign, target, debuff.Amount, rounds, creatures, remaining),
            _ => ScoreTerms.Zero,
        };
    }

    /// <summary>
    /// What a defense debuff is worth: the damage it lets through, the mirror of what <see cref="DefensiveTerms"/>
    /// reads a buff to prevent (ADR 0098). It takes off at most the defense the target holds, since total defense
    /// floors at zero, and the difference is the threat on the target with that defense gone over the threat on
    /// it now, over the debuff's rounds, shared across the target's living team the way a buff's is. A debuff on
    /// a creature with no defense lets nothing through and is worth nothing; on the actor's own side the same
    /// reading is the cost of `psycho_rush`'s recoil. A creature this resolution kills, or one already expected
    /// gone, neither attacks the target afterwards nor shares its team's load, so a lethal Psycho Rush pays its
    /// recoil only against the enemies left standing.
    /// </summary>
    private ScoreTerms DefenseDebuffTerms(
        int sign, CreatureSnapshot target, int amount, int rounds, IReadOnlyList<CreatureSnapshot> creatures, Dictionary<CreatureId, int> remaining)
    {
        var removed = Math.Min(amount, target.TotalDefense.Value);
        if (removed <= 0)
        {
            return ScoreTerms.Zero;
        }

        var standing = creatures.Where(creature => remaining[creature.Id] > 0).ToList();
        var team = standing.Count(creature => creature.Owner == target.Owner);
        var letThrough = ThreatOn(target, standing, -removed) - ThreatOn(target, standing, 0);
        return ScoreTerms.Zero with { Defense = sign * letThrough * rounds / Math.Max(1, team) };
    }

    /// <summary>
    /// A bleed at the bleed price, and, for the points a direct hit on the same target would lose to its
    /// defense, at the damage price too: a bleed ignores defense, so against a target that has stacked it the
    /// bleed is damage that gets through where a hit would not (ADR 0073). <paramref name="blocked"/> is the
    /// target's total defense over the bleed's rounds, one hit a round's worth. Against a target with no
    /// defense nothing is added and a bleed reads as it always has, so a board without defense scores as
    /// before; as the defense rises, so does what a bleed is worth beside a hit.
    /// </summary>
    private static ScoreTerms BleedTerms(int sign, int points, int blocked) =>
        ScoreTerms.Zero with { Bleed = sign * points, Damage = sign * Math.Min(points, blocked) };

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
    private ScoreTerms DefensiveTerms(CreatureSnapshot actor, CombatResolution resolution, IReadOnlyList<CreatureSnapshot> creatures, Dictionary<CreatureId, int> remaining, IReadOnlySet<CreatureId>? stillToAct)
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
            // Whether the round is lethal is read from the enemies still to act after this cast (ADR 0085); what
            // a buff prevents over its rounds is read from every enemy, since the rounds after this one are whole.
            var bare = ThreatOn(target, creatures, extraDefense: 0, stillToAct);
            var sign = Sign(actor, target);
            var stacked = 0;
            var prevented = 0.0;

            // Longest first, so each buff is priced on top of the ones that outlast it. A point past the ceiling
            // adds no defense and so prevents nothing (ADR 0076).
            var room = DefenseBuffRoom(target);
            foreach (var buff in Buffs(resolution, targetId).OrderByDescending(buff => buff.Duration.Rounds ?? PermanentConditionRounds))
            {
                var before = ThreatOn(target, creatures, Math.Min(stacked, room));
                stacked += buff.Amount;
                prevented += (before - ThreatOn(target, creatures, Math.Min(stacked, room))) * (buff.Duration.Rounds ?? PermanentConditionRounds);
            }

            terms = terms with { Defense = terms.Defense - (sign * prevented / Math.Max(1, allies)) };
            if (bare >= health && ThreatOn(target, creatures, Math.Min(stacked, room), stillToAct) < health + Restored(resolution, target, targetId))
            {
                terms = terms with { Kill = terms.Kill - sign };
            }
        }

        return terms;
    }

    /// <summary>
    /// How many more points of defense buff would still raise a creature's defense: the ceiling less the buffs
    /// it already holds, never below zero (ADR 0076).
    /// </summary>
    private static int DefenseBuffRoom(CreatureSnapshot target) => Math.Max(
        0,
        Creature.DefenseBuffCeiling - target.Conditions.Select(condition => condition.Effect).OfType<DefenseBuff>().Sum(buff => buff.Amount));

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
    /// costs exactly what it cost before. With <paramref name="stillToAct"/>, only the enemies among them count:
    /// the rest of this round's threat (ADR 0085).
    /// </summary>
    private double ThreatOn(CreatureSnapshot target, IReadOnlyList<CreatureSnapshot> creatures, int extraDefense, IReadOnlySet<CreatureId>? stillToAct = null)
    {
        var defense = target.TotalDefense.Value + extraDefense;
        var total = 0.0;
        foreach (var enemy in creatures)
        {
            if (enemy.Owner == target.Owner || enemy.IsDead || enemy.IsStunned || (stillToAct is not null && !stillToAct.Contains(enemy.Id)))
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
                    .Sum(damage => (chance * Landed(damage.Amount + enemy.DamageBonus, rules.CriticalMultiplier, defense)) + ((1 - chance) * Landed(damage.Amount + enemy.DamageBonus, 1.0, defense)));
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
