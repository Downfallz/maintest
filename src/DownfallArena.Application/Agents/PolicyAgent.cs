using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Plays a trained <see cref="PolicyFile"/>: every decision builds the observation of the board, lists the
/// candidate actions the options offer in the same order a dataset records them, scores each key with the
/// policy, and takes the best, the first on a tie. What the Python side's <c>Policy.choose</c> does, so a
/// policy plays identically on both sides.
/// </summary>
public sealed class PolicyAgent(PolicyFile policy, ObservationBuilder observations, ActionEncoder actions) : IPlayerAgent
{
    private static readonly Speed[] Speeds = [Speed.Quick, Speed.Standard];

    public PolicyFile Policy => policy;

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var slots = Slots(board);
        var candidates = options.Creatures
            .SelectMany(creature => creature.UnlockableSpells.Select(spell => new EvolutionChoice(creature.Creature, spell)))
            .Select(choice => (Decision: EvolutionDecision.Unlock(choice), Action: actions.Evolve(slots, choice)))
            .Append((Decision: EvolutionDecision.Pass, Action: ActionEncoder.Pass()));
        return Best(board, candidates);
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
    {
        ArgumentNullException.ThrowIfNull(board);

        var slots = Slots(board);
        return Best(board, Speeds.Select(speed => (speed, ActionEncoder.Speed(slots, new SpeedChoice(creature, speed)))));
    }

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(intentOption);

        if (intentOption.CastableSpells.Count == 0)
        {
            throw new InvalidOperationException("The options offer no castable spell.");
        }

        var slots = Slots(board);
        return Best(board, intentOption.CastableSpells.Select(spell => (spell, actions.Intent(slots, new CombatIntent(intentOption.Creature, spell)))));
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.LegalTargets.IsCastable)
        {
            return [];
        }

        var slots = Slots(board);
        return Best(board, TargetSets.Of(options.LegalTargets).Select(targets => (targets, actions.Targets(slots, options.Actor, options.Spell, targets))));
    }

    private BoardSlots Slots(PlayerBoardState board) => BoardSlots.Of(board, observations.Schema.TeamSize);

    /// <summary>The candidate whose key scores best on the observation; the first one on a tie.</summary>
    private TChoice Best<TChoice>(PlayerBoardState board, IEnumerable<(TChoice Choice, EncodedAction Action)> candidates)
    {
        var features = observations.Build(board).Features;
        var bestScore = double.NegativeInfinity;
        TChoice? best = default;
        var any = false;
        foreach (var (choice, action) in candidates)
        {
            var score = policy.Score(action.Key, features);
            if (!any || score > bestScore)
            {
                best = choice;
                bestScore = score;
                any = true;
            }
        }

        return any ? best! : throw new InvalidOperationException("The options offer nothing to choose from.");
    }
}
