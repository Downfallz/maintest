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
/// (ADR 0039): the reading <see cref="HeuristicAgent"/> takes before a declaration, kept apart so that the terms
/// a dataset records per candidate (ADR 0051) are read the same way. Binding targets needs no reading: an
/// action resolves as its targets are confirmed, so the board a creature aims on already carries every action
/// before it (ADR 0083).
/// </summary>
public sealed class Foresight(ActionScorer scorer)
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
    /// An intent carries a spell and no targets: targets are bound when its slot comes up. So the ally's target set is the
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
}
