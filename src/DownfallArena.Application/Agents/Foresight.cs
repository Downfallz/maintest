using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Agents;

/// <summary>
/// What a heuristic reading of the board can already tell about who will be dead before an action lands
/// (ADR 0039): the two readings <see cref="HeuristicAgent"/> takes before a declaration and before binding
/// targets, kept apart so that the terms a dataset records per candidate (ADR 0051) are read the same way.
/// </summary>
public sealed class Foresight(ActionScorer scorer, IGameResources resources, RuleSet rules)
{
    public static List<CreatureSnapshot> Creatures(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return [.. board.Allies, .. board.Enemies];
    }

    /// <summary>
    /// The enemies this actor's own team has already committed to killing before the actor acts (ADR 0039).
    /// <para>
    /// Both readings it needs are public and already on the board state, and the agent simply never looked at
    /// them: <c>Timeline</c> says who acts before whom, and <c>Intents</c> says what this player's creatures
    /// have declared so far this round. An ally counts only when it is <em>earlier on the timeline</em> and
    /// has <em>already declared</em> -- declaration order is not timeline order, so both tests are needed.
    /// </para>
    /// <para>
    /// An intent carries a spell and no targets: targets are bound at reveal. So the ally's target set is the
    /// one this same agent will pick for it, read one level deep and with an empty set of its own. Deeper
    /// would mean guessing how an ally reasons about a third ally, which is a different claim than reading
    /// what the board says. An ally earlier on the timeline that has not declared yet is skipped: unknown,
    /// and skipping it under-counts, which is the safe direction.
    /// </para>
    /// <para>
    /// Only the actor's own team is read. What the enemy will do is hidden until it is revealed, so bringing
    /// it in would be a guess about a hidden choice rather than a reading of public state.
    /// </para>
    /// </summary>
    public IReadOnlySet<CreatureId> AlreadyDoomed(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> creatures, CreatureSnapshot actor)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(actor);

        var position = IndexOnTimeline(board, actor.Id);
        if (position <= 0)
        {
            return ActionScorer.NoneGone;  // acts first, or is not on the timeline at all
        }

        var doomed = new HashSet<CreatureId>();
        foreach (var intent in board.Intents)
        {
            if (intent.Actor == actor.Id || IndexOnTimeline(board, intent.Actor) >= position)
            {
                continue;
            }

            var ally = creatures.FirstOrDefault(creature => creature.Id == intent.Actor);
            if (ally is null || !ally.IsAlive)
            {
                continue;
            }

            var targets = scorer.Best(ally, intent.Spell, creatures)?.Targets;
            if (targets is not null)
            {
                doomed.UnionWith(scorer.Kills(CombatAction.Bind(intent, targets), creatures, ActionScorer.NoneGone));
            }
        }

        return doomed;
    }

    /// <summary>Where a creature sits in the combat timeline, or -1 when it has no slot this round.</summary>
    private static int IndexOnTimeline(PlayerBoardState board, CreatureId creature)
    {
        for (var index = 0; index < board.Timeline.Count; index++)
        {
            if (board.Timeline[index].Creature == creature)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The creatures the actions already revealed this round will kill before this one resolves (ADR 0039).
    /// <para>
    /// Targets are bound in <c>RevealAndTarget</c>, a whole sub-phase before <c>ActionResolution</c>, so a
    /// creature choosing targets is looking at the board as it stood before combat: nothing has resolved yet,
    /// and every creature that dies during combat invalidates a target bound on it. That is 45.6 % of every
    /// fizzle in a greedy mirror, second only to the actor dying first, which no choice of target can help.
    /// </para>
    /// <para>
    /// What makes it readable is that <c>RevealedActions</c> holds the slots before this one, in timeline
    /// order and with their targets already bound — <em>both</em> teams, because a revealed action is public.
    /// So this is not a guess about a hidden choice: it replays what has been declared, in order, against a
    /// board it carries forward, and reports who does not survive it.
    /// </para>
    /// <para>
    /// On the plain roll, not the critical one: a target that only a critical would kill is not one to write
    /// off. The replay carries health forward and nothing else, which is enough for the case it exists for --
    /// attacks piling onto the same creature -- and not enough for anything that changes whether a later
    /// action happens or lands: a stun on its actor, a drain that takes it below its cost, a heal or a
    /// defense buff on its target. Rather than re-implement the whole of <c>CombatExecution</c> against
    /// snapshots, the replay **stops as soon as it would have to guess**: a creature touched by an outcome
    /// this reading does not model becomes uncertain, and the first action whose actor or targets are
    /// uncertain ends the replay. What comes back is therefore sound but incomplete, which is the safe
    /// direction — a creature wrongly left out is an opportunity missed, while a creature wrongly written off
    /// makes the actor pick a worse target on purpose.
    /// </para>
    /// </summary>
    public IReadOnlySet<CreatureId> GoneBeforeThisSlot(PlayerBoardState board, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(creatures);

        if (board.RevealedActions.Count == 0)
        {
            return ActionScorer.NoneGone;
        }

        var ahead = creatures.ToList();
        var dead = new HashSet<CreatureId>();
        var uncertain = new HashSet<CreatureId>();
        foreach (var action in board.RevealedActions)
        {
            if (uncertain.Contains(action.Actor) || action.Targets.Any(uncertain.Contains))
            {
                break;
            }

            // Forced plain, so the speed cannot move this reading; Standard is passed because the call needs one.
            var resolution = ResolutionRules.Resolve(action, ahead, resources, rules, ForcedRandom.NotCritical, Speed.Standard);
            if (resolution.Fizzled)
            {
                continue;
            }

            foreach (var hit in resolution.Outcomes.OfType<DamageOutcome>().GroupBy(outcome => outcome.Target))
            {
                var index = ahead.FindIndex(creature => creature.Id == hit.Key);
                var left = Math.Max(0, ahead[index].Health.Value - hit.Sum(outcome => outcome.Amount));
                ahead[index] = ahead[index] with { Health = Health.Of(left) };
                if (left == 0)
                {
                    dead.Add(hit.Key);
                }
            }

            // Everything else -- a stun, a heal, an energy move, any condition -- is not carried forward, so
            // whoever it touched can no longer be read.
            foreach (var outcome in resolution.Outcomes.Where(outcome => outcome is not DamageOutcome))
            {
                uncertain.Add(outcome.Target);
            }
        }

        return dead;
    }
}
