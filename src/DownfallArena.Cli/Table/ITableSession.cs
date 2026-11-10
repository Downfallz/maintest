using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One match played at a table, as the host needs it: whatever plays the match behind this, the routes, the
/// lobby and the recording ask the same things of it. It is the seam ADR 0159 moves the host across: a driver
/// blocked on its seats on one side (<see cref="DrivenSession" />), a match stepped over its journal on the other
/// (<see cref="SteppedSession" />).
/// </summary>
internal interface ITableSession : IDisposable
{
    /// <summary>
    /// Raised when what the seats read has changed without a question being asked or an entry being recorded
    /// to say so: what moves a table's version beside those (ADR 0119).
    /// </summary>
    event Action? Changed;

    MatchId MatchId { get; }

    /// <summary>The rule set this match is playing, the table's own rather than the host's (ADR 0081).</summary>
    RuleSet Rules { get; }

    /// <summary>Completes with the outcome, or with the failure that stopped the match.</summary>
    Task<Result<MatchOutcome>> Outcome { get; }

    bool IsOver { get; }

    /// <summary>Whether the first question has been asked, or is about to be (ADR 0092).</summary>
    bool HasBegun { get; }

    /// <summary>Begins the match, once. Saying it twice is nothing.</summary>
    void Begin();

    /// <summary>Who is deciding for a seat now, by the name a record calls them.</summary>
    string SeatedName(PlayerSlot slot);

    /// <summary>Whether the seat's own person is the one deciding for it now, rather than a bot holding it for them.</summary>
    bool PlayedByItsPerson(PlayerSlot slot);

    /// <summary>The swap a seat is waiting to make, or none.</summary>
    Swap? Pending(PlayerSlot slot);

    /// <summary>
    /// Hands a seat over from the top of a round, and writes it down where a table rebuilt later asks for it
    /// again. Not taken when the seat has reached that round.
    /// </summary>
    /// <param name="wanted">The name the pilot gave, as it is written down.</param>
    Task<SwapOutcome> SwapAsync(PlayerSlot slot, Occupant next, string wanted, int round);

    /// <summary>A seat's board, for where the match has got to and how it ended. What a seat reads to play is its snapshot.</summary>
    Task<Result<PlayerBoardState>> BoardAsync(PlayerSlot slot);

    /// <summary>What a seat reads, as one moment.</summary>
    Task<Result<SeatSnapshot>> SnapshotAsync(PlayerSlot slot);

    /// <summary>
    /// Hands a person's decision over, if it still answers the asking the caller names.
    /// </summary>
    /// <param name="offered">The options the decision was checked against.</param>
    /// <param name="round">The round the decision is made in, read before it is handed over.</param>
    Task<DecisionTaken> DecideAsync(PlayerSlot slot, PlayerDecision decision, long asked, PlayerOptions offered, int? round);

    /// <summary>
    /// The seat gives the match up (ADR 0087). Refused with <see cref="SeatErrors.PlayedByABot" /> by a session
    /// that finds a bot holding the seat when the concession reaches the match.
    /// </summary>
    Task<Result> ConcedeAsync(PlayerSlot slot);

    /// <summary>Completes once what a decision set off has been applied, so the trace read next holds it.</summary>
    /// <param name="traced">How long the trace was before the decision was handed over.</param>
    Task SettledAsync(PlayerSlot slot, int traced);
}
