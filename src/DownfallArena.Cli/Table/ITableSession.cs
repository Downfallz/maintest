using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One match played at a table, as the host needs it: what the routes, the lobby and the recording ask of the
/// match a table plays, which is stepped over its journal (<see cref="SteppedSession" />, ADR 0159). A test
/// stands between the two to move the match behind a read.
/// </summary>
internal interface ITableSession : IDisposable
{
    /// <summary>
    /// Raised when what the seats read has changed, other than a question asked of a seat's person, which the
    /// person says: what moves a table's version beside that and the outcome (ADR 0119).
    /// </summary>
    event Action? Changed;

    MatchId MatchId { get; }

    /// <summary>
    /// How many of the match's trace entries are of the match as its journal holds it: what a checkpoint keeps.
    /// Entries past it may be of a step that is never written. None where every entry recorded is the match's.
    /// </summary>
    int? Journaled { get; }

    /// <summary>The rule set this match is playing, the table's own rather than the host's (ADR 0081).</summary>
    RuleSet Rules { get; }

    /// <summary>Completes with the outcome, or with the failure that stopped the match.</summary>
    Task<Result<MatchOutcome>> Outcome { get; }

    bool IsOver { get; }

    /// <summary>Whether the first question has been asked, or is about to be (ADR 0092).</summary>
    bool HasBegun { get; }

    /// <summary>Begins the match, once. Saying it twice is nothing.</summary>
    void Begin();

    /// <summary>When this session began the match, by its host's clock; none before it has.</summary>
    DateTimeOffset? BegunAt { get; }

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
    Task<DecisionTaken> DecideAsync(PlayerSlot slot, PlayerDecision decision, long asked);

    /// <summary>
    /// The seat gives the match up (ADR 0087). Refused with <see cref="SeatErrors.PlayedByABot" /> by a session
    /// that finds a bot holding the seat when the concession reaches the match.
    /// </summary>
    Task<Result> ConcedeAsync(PlayerSlot slot);

    /// <summary>
    /// The people asked to decide now lose the match for letting the table's time to answer run out, or draw it when
    /// both are asked (ADR 0169). Who is asked is settled where the match ends; nobody asked, and nothing ends.
    /// </summary>
    Task<Result> TimeOutAsync();


    /// <summary>
    /// Reads the match again when its journal no longer ends where the match kept here does: a line another process
    /// wrote (ADR 0162). Whether it had moved; the pages are told as the match is taken.
    /// </summary>
    Task<bool> CatchUpAsync(CancellationToken cancellationToken = default);
}
