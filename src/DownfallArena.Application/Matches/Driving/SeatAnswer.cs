using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// A Player agent's answer to what a seat is asked, put the way a person's is submitted (ADR 0159): for whoever
/// decides a seat's question from outside the stepper and hands it in, a script setting a match up included.
/// </summary>
public static class SeatAnswer
{
    /// <summary>The agent's decision on the seat's question, its board and its options as the seat reads them.</summary>
    public static PlayerDecision Of(IPlayerAgent agent, SeatView view)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(view);
        var asked = view.Asked ?? throw new InvalidOperationException("The seat is not asked anything.");
        var (board, options) = (view.Board, view.Options);
        switch (asked.Kind)
        {
            case PlayerOptionsKind.Evolution:
                return agent.DecideEvolution(board, Section(options.Evolution)).Choice is { } choice
                    ? PlayerDecision.Buy(choice.Creature, choice.Tier)
                    : PlayerDecision.Pass;
            case PlayerOptionsKind.Speed:
                var creature = asked.Creature ?? throw new InvalidOperationException("A speed is asked of one creature.");
                return PlayerDecision.ChooseSpeed(creature, agent.DecideSpeed(board, creature));
            case PlayerOptionsKind.TieOrder:
                return PlayerDecision.OrderTies(agent.DecideTieOrder(board, Section(options.TieOrder)));
            case PlayerOptionsKind.Intent:
                var option = Section(options.Intent).Creatures.Single(offered => offered.Creature == asked.Creature);
                return PlayerDecision.DeclareIntent(option.Creature, agent.DecideIntent(board, option));
            case PlayerOptionsKind.Target:
                return PlayerDecision.BindTargets(agent.DecideTargets(board, Section(options.Target)));
            case PlayerOptionsKind.Action:
                var action = Section(options.Action);
                var chosen = agent.DecideAction(board, action);
                return PlayerDecision.Act(action.Actor, chosen.Spell, chosen.Targets);
            default:
                throw new InvalidOperationException($"A seat is not asked {asked.Kind}.");
        }
    }

    private static TSection Section<TSection>(TSection? section)
        where TSection : class =>
        section ?? throw new InvalidOperationException($"The options announce a decision but carry no {typeof(TSection).Name}.");
}
