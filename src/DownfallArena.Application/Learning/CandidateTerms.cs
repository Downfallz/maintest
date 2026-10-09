using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The scorer's terms of every candidate action a decision offers, in the order <see cref="ActionEncoder.Candidates"/>
/// lists them (ADR 0051): what the heuristic agents read before they weigh it, handed to a dataset beside the
/// observation and to a policy that carries weights over the terms. The observation says what the board is;
/// the terms say what each action would do to it, which no linear row over the board can express.
/// <para>
/// Read the way <see cref="HeuristicAgent"/> reads: an intent's terms are those of the target set the built-in
/// weights would bind for it, with the allies' declared kills already written off; a target set's terms are
/// its own, on a board that already carries the actions before it (ADR 0083); an unlock's terms are its combat
/// estimate plus the initiative it buys and the cost it cannot cover. A speed choice and a pass have no reading
/// and score zero on every term.
/// </para>
/// <para>
/// The reading is the built-in weights' whatever agent is recorded or played, so a dataset and the policy
/// trained on it read the same numbers. An intent's terms are therefore one fixed target set's, the one
/// Greedy would bind, and a heuristic agent playing other weights may bind another: its intent scores best
/// under its own weights among the terms it read, not always among the terms recorded here.
/// </para>
/// </summary>
public sealed class CandidateTerms(IGameResources resources, RuleSet rules)
{
    private readonly ActionScorer _scorer = new(resources, rules, ScoringWeights.Default);
    private readonly Foresight _foresight = new(new ActionScorer(resources, rules, ScoringWeights.Default).WithoutUnlocks);

    /// <summary>The name of each term, in the order every vector below lists them: the weight names.</summary>
    public static IReadOnlyList<string> Names => ScoreTerms.Names;

    /// <summary>One vector per unlock in the options' order, then the pass, which reads zero.</summary>
    public IReadOnlyList<IReadOnlyList<float>> Evolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        var creatures = Foresight.Creatures(board);
        var terms = new List<IReadOnlyList<float>>();
        foreach (var option in options.Creatures)
        {
            var actor = creatures.First(creature => creature.Id == option.Creature);
            terms.AddRange(option.AvailableTiers.Select(tier => Vector(_scorer.PurchaseTerms(actor, tier, creatures))));
        }

        terms.Add(Vector(ScoreTerms.Zero));
        return terms;
    }

    /// <summary>Quick and Standard, in that order, neither of which the scorer reads.</summary>
    public static IReadOnlyList<IReadOnlyList<float>> Speed() => [Vector(ScoreTerms.Zero), Vector(ScoreTerms.Zero)];

    /// <summary>
    /// One vector per castable spell in the option's order: the terms of the target set the heuristic agent binds,
    /// a winning one first (ADR 0099). The win itself is not a term -- no weight could be trusted to dominate it --
    /// so a policy on these terms reads the end of a match the way the scorer did before.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<float>> Intent(PlayerBoardState board, IntentOption option)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(option);

        var creatures = Foresight.Creatures(board);
        var actor = creatures.First(creature => creature.Id == option.Creature);
        var gone = _foresight.AlreadyDoomed(board, creatures, actor);
        var stillToAct = Foresight.StillToAct(board, actor.Id);
        // Nothing to hit is worth nothing (ADR 0040), as the heuristic agent reads it.
        return [.. option.CastableSpells.Select(spell => Vector(
            _scorer.Best(actor, spell, creatures, gone, SpeedOf(board, actor.Id), stillToAct, board.RoundNumber) is { } best
                ? _scorer.ExpectedTerms(CombatAction.Bind(new CombatIntent(actor.Id, spell), best.Targets), creatures, gone, SpeedOf(board, actor.Id), stillToAct, board.RoundNumber)
                : ScoreTerms.Zero))];
    }

    /// <summary>One vector per legal target set in <see cref="TargetSets"/> order; one zero vector for an uncastable spell.</summary>
    public IReadOnlyList<IReadOnlyList<float>> Targets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.LegalTargets.IsCastable)
        {
            return [Vector(ScoreTerms.Zero)];
        }

        var creatures = Foresight.Creatures(board);
        var stillToAct = Foresight.StillToAct(board, options.Actor);
        return [.. TargetSets.Of(options.LegalTargets).Select(targets => Vector(_scorer.ExpectedTerms(CombatAction.Bind(new CombatIntent(options.Actor, options.Spell), targets), creatures, speed: SpeedOf(board, options.Actor), stillToAct: stillToAct, round: board.RoundNumber)))];
    }

    /// <summary>
    /// What the creature chose in the Speed sub-phase, which decides whether its cast can crit: the reading the
    /// heuristic agent decides on, so the terms recorded beside its decision are the ones it compared.
    /// </summary>
    private static Domain.Matches.Rounds.Speed SpeedOf(PlayerBoardState board, CreatureId creature) =>
        board.SpeedChoices.FirstOrDefault(choice => choice.Creature == creature)?.Speed ?? Domain.Matches.Rounds.Speed.Standard;

    private static float[] Vector(ScoreTerms terms) => [.. terms.ToArray().Select(value => (float)value)];
}
