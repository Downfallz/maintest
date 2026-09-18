using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A bot that hands its seat to a person at the top of a round. Until then it plays; from there it seats the
/// person and gives them the very question it was just asked, rather than answering one last time for them.
/// </summary>
/// <remarks>
/// The highest-value piloting item of stage 1, and three lines on top of a seat that delegates: the tenth
/// round is the one nobody reaches by hand, and it is exactly the one a playtest needs to read
/// (<c>docs/tabletop/app-roadmap.md</c>). It needs no clock and no polling — every decision the match asks
/// carries the board, and the board carries the round.
/// </remarks>
internal sealed class HandoverAgent(SeatAgent seat, Occupant bot, Occupant person, int round) : IPlayerAgent, IRouteDecisions
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) =>
        Deciding(board).Agent.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        Deciding(board).Agent.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) =>
        Deciding(board).Agent.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) =>
        Deciding(board).Agent.DecideTargets(board, options);

    /// <summary>Who this board goes to, worked out and nothing else: no seating, no decision.</summary>
    public Occupant DeciderOf(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.RoundNumber is { } current && current >= round ? person : bot;
    }

    private Occupant Deciding(PlayerBoardState board)
    {
        var deciding = DeciderOf(board);
        if (deciding == bot)
        {
            return bot;
        }

        // Seated for every question after this one, and asked this one too: handing over a round late would
        // give away the first decision of the round the playtest is here to read.
        seat.Seat(person);
        return person;
    }
}
