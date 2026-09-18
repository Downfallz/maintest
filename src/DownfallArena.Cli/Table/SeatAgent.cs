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
    private int? _reached;

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
    /// Hands the seat over at the top of a round rather than now, or refuses because that round is already
    /// being played.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why a round and not an instant: the driver asks one seat for several decisions inside one sub-phase --
    /// a speed for each creature, then an intent for each -- so a seat that changed hands between two
    /// creatures of the same team would split that sub-phase between two players and make the round
    /// unreadable (<c>docs/tabletop/app-roadmap.md</c>, stage 6; <c>MatchDriver.ActAsync</c>).
    /// </para>
    /// <para>
    /// Why the round is checked <em>here</em> and not by whoever asks: the check and the install have to be
    /// one step against a match that is moving. A caller that read the board, found round 5 and then asked for
    /// 6 can be overtaken between the two -- resolving a file-backed agent alone is long enough -- and the
    /// driver is then already inside round 6 when the swap arrives, which is the split this refuses. The seat
    /// sees every round as it is asked about it, so under this lock the newest round it has been asked for is
    /// the newest round there is.
    /// </para>
    /// <para>
    /// A seat holds one pending swap. A second replaces the first, which is what an operator changing their
    /// mind means, and is the only reading that keeps "who is seated" answerable without walking a chain.
    /// </para>
    /// </remarks>
    public SwapOutcome SwapAt(Occupant next, int round)
    {
        ArgumentNullException.ThrowIfNull(next);
        lock (_gate)
        {
            // Nothing has been asked of this seat yet, so no round has begun for it and any round from the
            // first will do. That is what makes `--handover 1` a swap like any other rather than a special
            // case -- and why the floor is checked here too: without it a round of zero would be "not yet
            // reached" and would land on the seat's very first decision, which is not what naming a round
            // means.
            if (round < FirstRound || (_reached is { } reached && round <= reached))
            {
                return new SwapOutcome(_seated, _reached, Taken: false);
            }

            _swap = new Swap(next, round);
            return new SwapOutcome(_seated, _reached, Taken: true);
        }
    }

    /// <summary>The first round a match has, and so the earliest a seat can be named for.</summary>
    private const int FirstRound = 1;

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
            if (board.RoundNumber is { } round)
            {
                // The newest round this seat has been asked about, which is what a swap is refused against.
                _reached = _reached is { } seen && seen > round ? seen : round;
                if (_swap is { } swap && round >= swap.Round)
                {
                    _seated = swap.Next;
                    _swap = null;
                }
            }

            return new Decider(_seated.Agent, _seated.Name);
        }
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Deciding(board).Agent.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Deciding(board).Agent.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Deciding(board).Agent.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Deciding(board).Agent.DecideTargets(board, options);
}
