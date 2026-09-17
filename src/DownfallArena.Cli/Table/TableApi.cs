using System.Globalization;
using System.Text.Json;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
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
internal sealed class TableApi(TableSession session, MatchQueryHandlers queries, IReadOnlyList<TableSeat> seats, CatalogueView catalogue)
{
    public const string TokenHeader = "X-Seat-Token";

    private const string SeatPrefix = "/api/seat/";

    /// <summary>
    /// An entity tag over everything the answer carries: the content hash and the rule set. The catalogue
    /// cannot change while a host is running, so a page fetches it once and is answered 304 ever after — but a
    /// host restarted on the same port with the same content and a different <c>--rules</c> file is a different
    /// answer, and a tag of the hash alone would let a browser keep the old rule set (ADR 0054: a session is
    /// reproducible against the pair, not against either half).
    /// </summary>
    private string Tag => $"\"{catalogue.ContentHash}+{catalogue.Rules.TeamSize}-{catalogue.Rules.EnergyPerRound}-{catalogue.Rules.EvolutionPicksPerRound}-{catalogue.Rules.RoundCap}-{catalogue.Rules.CriticalMultiplier.ToString(CultureInfo.InvariantCulture)}\"";

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token, string? ifNoneMatch = null)
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
            ("GET", [_]) => await SeatAsync(holder),
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
        if (string.Equals(ifNoneMatch, Tag, StringComparison.Ordinal))
        {
            return new StudioResponse(304, StudioResponse.Plain, [], [new KeyValuePair<string, string>("ETag", Tag)]);
        }

        return StudioResponse.OfJson(catalogue, ArtifactJson.LineOptions) with
        {
            Headers = [new KeyValuePair<string, string>("ETag", Tag)],
        };
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

    private async Task<StudioResponse> SeatAsync(TableSeat seat)
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
