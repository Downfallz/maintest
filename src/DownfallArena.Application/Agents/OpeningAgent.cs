using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// Plays its inner agent, except that its picks of round 1 buy the packages it was told to open with (ADR 0109):
/// <c>opening:brute+occultist</c> opens Greedy's team on one Brute and one Warped, and a third segment names
/// another inner agent, as <c>explore:</c> does. Each pick of the round buys the next package of the list that is
/// still to buy, on whichever creature the inner agent prefers among those that can buy it; once the list is
/// bought, or when no creature can buy what is left of it, the inner agent decides alone. Everything after
/// round 1 is the inner agent's, so what is measured is what the opening is worth to that player.
/// </summary>
public sealed class OpeningAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;

    public OpeningAgent(IReadOnlyList<TierId> opening, IPlayerAgent inner)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (opening.Count == 0)
        {
            throw new ArgumentException("An opening names at least one package.", nameof(opening));
        }

        Opening = opening;
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>The packages round 1 buys, in the order its picks buy them.</summary>
    public IReadOnlyList<TierId> Opening { get; }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(options);
        if (board.RoundNumber != 1 || Next(board) is not { } next || Restricted(options, next) is not { } restricted)
        {
            return _inner.DecideEvolution(board, options);
        }

        // An inner agent that may pass (a policy, an exploring agent) would leave the opening unbought, and the
        // reading would no longer be the opening it is named for.
        var decision = _inner.DecideEvolution(board, restricted);
        return decision.Choice is null
            ? EvolutionDecision.Unlock(new EvolutionChoice(restricted.Creatures[0].Creature, next))
            : decision;
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => _inner.DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => _inner.DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => _inner.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => _inner.DecideTargets(board, options);

    /// <summary>
    /// The next package of the opening the round has not bought yet, or <c>null</c> once it is all bought: the
    /// round's picks so far are taken off the list one for one, so an opening of two Brutes buys Brute twice.
    /// </summary>
    private TierId? Next(PlayerBoardState board)
    {
        var left = Opening.ToList();
        foreach (var choice in board.EvolutionChoices)
        {
            left.Remove(choice.Tier);
        }

        return left.Count == 0 ? null : left[0];
    }

    /// <summary>The options narrowed to that package, or <c>null</c> when no creature can buy it.</summary>
    private static EvolutionOptions? Restricted(EvolutionOptions options, TierId next)
    {
        var able = options.Creatures
            .Where(option => option.AvailableTiers.Contains(next))
            .Select(option => option with { AvailableTiers = [next] })
            .ToList();
        return able.Count == 0 ? null : options with { Creatures = able };
    }
}
