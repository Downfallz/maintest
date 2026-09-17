using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Feed;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's JSON API: what the session is, what one seat sees, and the one decision that seat can submit.
/// </summary>
/// <remarks>
/// Three things it will not do, and they are the reason it exists rather than the page talking to the engine
/// directly. It never answers two seats in one payload, so a boundary the client could ignore is a boundary
/// the server keeps. It never decides: an answer is checked against the options that seat was actually served
/// (<see cref="PlayerDecisionCheck" />) and then handed to the person's own seat, so the aggregate's gates are
/// still the only thing that says what is legal. And it never invents a refusal: a tap that arrives after the
/// screen moved on is late, not illegal, and says so with a 409 the page can act on.
/// </remarks>
internal sealed class TableApi(TableSession session, MatchQueryHandlers queries, IReadOnlyList<TableSeat> seats, CatalogueView catalogue, MatchTraceRecorder events)
{
    public const string TokenHeader = "X-Seat-Token";

    private const string SeatPrefix = "/api/seat/";

    /// <summary>
    /// The catalogue as it is served, and an entity tag over the bytes themselves. The catalogue cannot change
    /// while a host is running, so both are computed once and a page that fetches it is answered 304 ever
    /// after.
    /// </summary>
    /// <remarks>
    /// The tag is a hash of the representation rather than a list of the things that went into it, and that is
    /// the whole point: this answer carries the content hash, the rule set and this build's projection of both
    /// — the card words <c>CatalogueProjection</c> writes and the round shape it reads off
    /// <c>RoundSubPhase</c> — so any tag assembled from stamps has to be kept in step with whatever the
    /// projection grows next, and was not (ADR 0054 asks only that a session be reproducible against the
    /// content *and* the rule set, which is a smaller claim than this tag has to make). The engine version
    /// cannot close it either: a dirty tree stamps <c>&lt;commit&gt;-dirty</c> whatever is edited in it, which
    /// is exactly the state this app is developed in. Hashing the bytes is right by construction, and it also
    /// means the catalogue is serialized once for the life of the host rather than once a request.
    /// </remarks>
    private readonly Lazy<(byte[] Body, string Tag)> _served = new(() =>
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(catalogue, ArtifactJson.LineOptions);
        return (body, $"\"{Convert.ToHexStringLower(SHA256.HashData(body))}\"");
    });

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token, string? ifNoneMatch = null, string? query = null)
    {
        if (Holder(token) is not { } holder)
        {
            return StudioResponse.OfPlainText(403, $"Every request carries the seat's own '{TokenHeader}'.");
        }

        if (path == "/api/session" && method == "GET")
        {
            return Session();
        }

        if (path == "/api/catalogue" && method == "GET")
        {
            return Catalogue(ifNoneMatch);
        }

        if (!path.StartsWith(SeatPrefix, StringComparison.Ordinal))
        {
            return StudioResponse.OfPlainText(404, $"No such route: {path}");
        }

        var segments = path[SeatPrefix.Length..].Split('/');
        if (TableSeat.SlotOf(segments[0]) is not { } slot)
        {
            return StudioResponse.OfPlainText(404, $"'{segments[0]}' is neither player1 nor player2.");
        }

        // The token names a seat, so asking for another one is refused whatever the path says. In hotseat both
        // tokens sit in one browser, which is why this is checked rather than assumed.
        if (slot != holder.Slot)
        {
            return StudioResponse.OfPlainText(403, $"This token is {holder.Name}'s; it cannot read or act for {TableSeat.NameOf(slot)}.");
        }

        return (method, segments) switch
        {
            ("GET", [_]) => await SeatAsync(holder, Since(query)),
            ("POST", [_, "decision"]) => await DecideAsync(holder, body),
            _ => StudioResponse.OfPlainText(404, $"No such route: {method} {path}"),
        };
    }

    /// <summary>
    /// Every card of the catalogue the match is playing. It is the same for both seats and hides nothing: a
    /// deck is public, and what is hidden is which card a creature has face down (ADR 0054).
    /// </summary>
    private StudioResponse Catalogue(string? ifNoneMatch)
    {
        var (body, tag) = _served.Value;
        return string.Equals(ifNoneMatch, tag, StringComparison.Ordinal)
            ? new StudioResponse(304, StudioResponse.Plain, [], [new KeyValuePair<string, string>("ETag", tag)])
            : new StudioResponse(200, StudioResponse.Json, body, [new KeyValuePair<string, string>("ETag", tag)]);
    }

    private static PlayerSlot Other(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

    /// <summary>
    /// How many of a board's intents are still face down. A round keeps every intent it was given
    /// (<c>Round.IntentsOf</c>) and tracks what has been turned over separately, in the actions the reveal
    /// cursor has reached, so the count of the one is not the count of the other from the first reveal
    /// onwards. Subtracting here is what keeps the number and the reveal strip from contradicting each other
    /// on the same screen: three face down beside two of them face up is a table nobody would believe.
    /// </summary>
    private static int StillFaceDown(PlayerBoardState board)
    {
        var revealed = board.RevealedActions.Select(action => action.Actor).ToHashSet();
        return board.Intents.Count(intent => !revealed.Contains(intent.Actor));
    }

    private TableSeat? Holder(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : seats.FirstOrDefault(seat => string.Equals(seat.Token, token, StringComparison.Ordinal));

    /// <summary>What both seats may know: whose match this is, and whether it is over.</summary>
    private StudioResponse Session()
    {
        var outcome = session.IsOver && session.Outcome.IsCompletedSuccessfully && session.Outcome.Result.IsSuccess
            ? session.Outcome.Result.Value
            : null;

        return StudioResponse.OfJson(
            new { matchId = session.MatchId, over = session.IsOver, outcome },
            ArtifactJson.LineOptions);
    }

    /// <summary>
    /// The sequence number a page has already seen everything below, off <c>?since=N</c>. Anything that is not
    /// a number is nothing asked for: a feed from the start is the right answer to a query nobody meant.
    /// </summary>
    private static int Since(string? query)
    {
        var since = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)["since"];
        return int.TryParse(since, CultureInfo.InvariantCulture, out var sequence) && sequence > 0 ? sequence : 0;
    }

    private async Task<StudioResponse> SeatAsync(TableSeat seat, int since)
    {
        var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, seat.Slot));
        if (board.IsFailure)
        {
            return StudioResponse.OfPlainText(500, board.Error.Message);
        }

        var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(session.MatchId, seat.Slot));
        if (options.IsFailure)
        {
            return StudioResponse.OfPlainText(500, options.Error.Message);
        }

        // How many cards the other side has face down, and nothing else about them. At a table that number is
        // public -- face-down cards are countable -- so it is served, as a count computed here rather than as
        // a list the client is trusted not to read (ADR 0054).
        var opponent = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, Other(seat.Slot)));

        // Everything the host looked at this poll: the slice from the cursor onwards. What the seat may be told
        // of it is a subset, and where to resume is the end of the slice rather than the end of that subset.
        var examined = events.EntriesOf(session.MatchId, since);

        // The question the seat is blocked on, rather than what the sub-phase allows: a person acts when their
        // own seat is asked, and the two differ while the other seat is still deciding.
        var waiting = seat.Person?.Waiting;
        return StudioResponse.OfJson(
            new
            {
                seat = seat.Name,
                board = board.Value,
                options = options.Value,
                waitingFor = waiting?.Kind.ToString(),
                waitingCreature = waiting?.Creature,
                playedByBot = seat.Person is null,
                over = session.IsOver,
                opponentIntents = opponent.IsSuccess ? StillFaceDown(opponent.Value) : 0,

                // What has happened, as this seat may be told it: the boards the trace keeps beside every
                // event are dropped here and the other seat's hidden decisions never reach the wire
                // (SeatVisibility). The trace itself is not served during a session.
                feed = SeatFeedProjection.Build(examined, seat.Slot, since),

                // Where to resume, said by the host rather than inferred by the page from what it can see.
                // A seat's own decisions are filtered out of the other seat's feed, so a run of them at the
                // end of the trace reaches nobody -- and a page taking its cursor from the highest entry it
                // was shown would sit before that run, asking for it again every poll for as long as the
                // other player took to decide. This is the end of what the host looked at, which is the
                // thing that actually bounds the work.
                feedNext = since + examined.Count,
            },
            ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> DecideAsync(TableSeat seat, string body)
    {
        if (seat.Person is not { } person)
        {
            return StudioResponse.OfPlainText(409, $"{seat.Name} is played by a bot; it decides for itself.");
        }

        TableDecisionBody? posted;
        try
        {
            posted = JsonSerializer.Deserialize<TableDecisionBody>(body, ArtifactJson.LineOptions);
        }
        catch (JsonException exception)
        {
            return StudioResponse.OfPlainText(400, exception.Message);
        }

        if (posted is null)
        {
            return StudioResponse.OfPlainText(400, $"A decision names one of: {TableDecisionBody.Kinds}.");
        }

        if (posted.ToDecision(out var problem) is not { } decision)
        {
            return StudioResponse.OfPlainText(400, problem);
        }

        var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(session.MatchId, seat.Slot));
        if (options.IsFailure)
        {
            return StudioResponse.OfPlainText(500, options.Error.Message);
        }

        var check = PlayerDecisionCheck.Validate(options.Value, decision);
        if (check.IsFailure)
        {
            return StudioResponse.OfJson(new { error = check.Error.Code, message = check.Error.Message }, ArtifactJson.LineOptions, status: 409);
        }

        // Checked against the options, and still refused: the seat moved on between the two. That is the race
        // the driver would have thrown on, answered as the late tap it is.
        return person.Submit(decision)
            ? new StudioResponse(204, StudioResponse.Plain, [])
            : StudioResponse.OfJson(new { error = "Seat.NotWaiting", message = "This seat is not waiting for that decision any more." }, ArtifactJson.LineOptions, status: 409);
    }
}
