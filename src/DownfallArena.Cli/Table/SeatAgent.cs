using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
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
    /// Who to ask for this board and what a record should call them, as one reading of the seat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One reading is the whole point. Naming the occupant and then asking the seat again are two reads of
    /// something another thread can change in between -- the pilot swaps a seat while the driver is deciding
    /// -- and a swap landing between them writes one occupant's name on another's decision. Taking the
    /// occupant once and handing out both means the answer and the name always describe the same player. A
    /// swap that arrives after this read is a swap that arrives after this question, which is what a seat
    /// already promises.
    /// </para>
    /// <para>
    /// The agent handed out is the occupant itself and not this seat, because calling the seat would be the
    /// second read this exists to avoid. The name may still come from further down: an occupant that routes
    /// by the board rather than playing -- a handover holds both players -- is asked who it would route to,
    /// by the same rule that will route the decision.
    /// </para>
    /// </remarks>
    public Decider Deciding(PlayerBoardState board)
    {
        var seated = Seated;
        var decider = seated.Agent is IRouteDecisions router ? router.DeciderOf(board) : seated;
        return new Decider(seated.Agent, decider.Name);
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Seated.Agent.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Seated.Agent.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Seated.Agent.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Seated.Agent.DecideTargets(board, options);
}
