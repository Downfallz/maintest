using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A seat at the table. It decides nothing: it forwards every decision to whoever is seated, and who is
/// seated can change while the match runs.
/// </summary>
/// <remarks>
/// Built as a delegate from the first commit, because every piloting capability the instrument needs is this
/// swap and nothing else: seating a bot opposite a person, handing a seat over mid-match, letting bots play
/// the early rounds and taking over at the tenth, which is the round nobody reaches by hand. A seat that
/// decided for itself would have to grow each of those as a feature instead
/// (<c>docs/tabletop/app-roadmap.md</c>, stage 1).
/// </remarks>
internal sealed class SeatAgent : IPlayerAgent
{
    private Occupant _seated;

    public SeatAgent(Occupant seated)
    {
        ArgumentNullException.ThrowIfNull(seated);
        _seated = seated;
    }

    /// <summary>Who is sitting here right now, and the name a record calls them by.</summary>
    public Occupant Seated => Volatile.Read(ref _seated);

    /// <summary>
    /// Hands the seat to somebody else and returns who held it. It takes effect at the next decision the match
    /// asks of this seat: a swap made while the seat is blocked on a person leaves that question with them.
    /// </summary>
    public Occupant Seat(Occupant next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return Interlocked.Exchange(ref _seated, next);
    }

    /// <summary>
    /// Who would decide this board, without deciding it: whoever is seated, or whoever they would pass it to.
    /// </summary>
    /// <remarks>
    /// A seat can hold an occupant that routes by the board rather than playing -- a handover plays the early
    /// rounds as a bot and the rest as a person, and holds both. Naming the router would name neither of them,
    /// so the question is passed down to it and answered by the same rule that would route the decision.
    /// </remarks>
    public Occupant DeciderOf(PlayerBoardState board)
    {
        var seated = Seated;
        return seated.Agent is IRouteDecisions router ? router.DeciderOf(board) : seated;
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Seated.Agent.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Seated.Agent.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Seated.Agent.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Seated.Agent.DecideTargets(board, options);
}
