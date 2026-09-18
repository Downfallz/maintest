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
    private readonly Lock _gate = new();
    private Occupant _seated;
    private Swap? _swap;

    public SeatAgent(Occupant seated)
    {
        ArgumentNullException.ThrowIfNull(seated);
        _seated = seated;
    }

    /// <summary>Who is sitting here right now, and the name a record calls them by.</summary>
    public Occupant Seated
    {
        get
        {
            lock (_gate)
            {
                return _seated;
            }
        }
    }

    /// <summary>The swap this seat is waiting to make, or none.</summary>
    public Swap? Pending
    {
        get
        {
            lock (_gate)
            {
                return _swap;
            }
        }
    }

    /// <summary>
    /// Hands the seat to somebody else now and returns who held it. It takes effect at the next decision the
    /// match asks of this seat: a swap made while the seat is blocked on a person leaves that question with
    /// them.
    /// </summary>
    public Occupant Seat(Occupant next)
    {
        ArgumentNullException.ThrowIfNull(next);
        lock (_gate)
        {
            var held = _seated;
            _seated = next;
            _swap = null;
            return held;
        }
    }

    /// <summary>
    /// Hands the seat over at the top of a round rather than now, and returns the swap this replaces.
    /// </summary>
    /// <remarks>
    /// Why a round and not an instant: the driver asks one seat for several decisions inside one sub-phase --
    /// a speed for each creature, then an intent for each -- so a seat that changed hands between two
    /// creatures of the same team would split that sub-phase between two players and make the round
    /// unreadable (<c>docs/tabletop/app-roadmap.md</c>, stage 6; <c>MatchDriver.ActAsync</c>). Naming a round
    /// instead of an instant is what makes that impossible rather than unlikely: the board carries the round,
    /// so the swap lands where a round begins however long the request waited.
    /// <para>
    /// A seat holds one pending swap. A second replaces the first, which is what an operator changing their
    /// mind means, and is the only reading that keeps "who is seated" answerable without walking a chain.
    /// </para>
    /// </remarks>
    public Swap? SwapAt(Occupant next, int round)
    {
        ArgumentNullException.ThrowIfNull(next);
        lock (_gate)
        {
            var replaced = _swap;
            _swap = new Swap(next, round);
            return replaced;
        }
    }

    /// <summary>
    /// Who to ask for this board and what a record should call them, as one reading of the seat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One reading is the whole point. Naming the occupant and then asking the seat again are two reads of
    /// something another thread can change in between -- the pilot swaps a seat while the driver is deciding
    /// -- and a swap landing between them writes one occupant's name on another's decision. Taking the
    /// occupant once and handing out both means the answer and the name always describe the same player.
    /// </para>
    /// <para>
    /// A pending swap is also applied here, under the same lock, because this is the only moment that holds
    /// both the board and the seat: the board says which round the match has reached, and the seat is what the
    /// swap changes. Applying it anywhere else would be a decision about a round taken without one.
    /// </para>
    /// </remarks>
    public Decider Deciding(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        lock (_gate)
        {
            if (_swap is { } swap && board.RoundNumber is { } round && round >= swap.Round)
            {
                _seated = swap.Next;
                _swap = null;
            }

            return new Decider(_seated.Agent, _seated.Name);
        }
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Deciding(board).Agent.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Deciding(board).Agent.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Deciding(board).Agent.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Deciding(board).Agent.DecideTargets(board, options);
}
