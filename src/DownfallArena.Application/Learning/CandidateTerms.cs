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
/// its own, with the revealed actions' kills written off; an unlock's terms are its combat estimate plus the
/// initiative it buys and the cost it cannot cover. A speed choice and a pass have no reading and score zero
/// on every term.
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
    private readonly Foresight _foresight = new(new ActionScorer(resources, rules, ScoringWeights.Default), resources, rules);

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

    /// <summary>One vector per castable spell in the option's order: the terms of its best target set.</summary>
    public IReadOnlyList<IReadOnlyList<float>> Intent(PlayerBoardState board, IntentOption option)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(option);

        var creatures = Foresight.Creatures(board);
        var actor = creatures.First(creature => creature.Id == option.Creature);
        var gone = _foresight.AlreadyDoomed(board, creatures, actor);
        // Nothing to hit is worth nothing (ADR 0040), as the heuristic agent reads it.
        return [.. option.CastableSpells.Select(spell => Vector(_scorer.BestTerms(actor, spell, creatures, gone)?.Terms ?? ScoreTerms.Zero))];
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
        var gone = _foresight.GoneBeforeThisSlot(board, creatures);
        return [.. TargetSets.Of(options.LegalTargets).Select(targets => Vector(_scorer.ExpectedTerms(CombatAction.Bind(new CombatIntent(options.Actor, options.Spell), targets), creatures, gone)))];
    }

    private static float[] Vector(ScoreTerms terms) => [.. terms.ToArray().Select(value => (float)value)];
}
