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
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Primitives;

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
internal sealed class TableApi(TableSession session, MatchQueryHandlers queries, IReadOnlyList<TableSeat> seats, CatalogueView catalogue, MatchTraceRecorder events, PlaytestRun? run = null, TablePilot? pilot = null)
{
    public const string TokenHeader = "X-Seat-Token";

    /// <summary>
    /// Whether the match reached an outcome. Stronger than "stopped being played" on purpose: a driver task
    /// that faulted or was cancelled is also complete, and the session page is fenced on this because the
    /// trace it carries holds both seats' boards. Nothing about a match that broke says the hidden half of it
    /// may now be read.
    /// </summary>
    public bool IsDecided => Decided is not null;

    private MatchOutcome? Decided =>
        session.IsOver && session.Outcome.IsCompletedSuccessfully && session.Outcome.Result.IsSuccess
            ? session.Outcome.Result.Value
            : null;

    private const string SeatPrefix = "/api/seat/";

    private const string PilotPrefix = "/api/pilot/seats/";

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
        // The pilot's routes are answered before a seat token is even looked for: the two tokens name
        // different authorities, and a request that carries one must not be measured against the other.
        if (path.StartsWith(PilotPrefix, StringComparison.Ordinal))
        {
            return await PilotAsync(method, path[PilotPrefix.Length..], body, token);
        }

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

        if (path == "/api/notes")
        {
            return method == "POST"
                ? await NoteAsync(holder, body)
                : StudioResponse.OfPlainText(404, $"No such route: {method} {path}");
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
            ("GET", [_]) => await SeatAsync(holder, Since(query), Shown(query)),
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
    private StudioResponse Session() =>
        StudioResponse.OfJson(
            new { matchId = session.MatchId, over = session.IsOver, outcome = Decided },
            ArtifactJson.LineOptions);

    /// <summary>
    /// The sequence number a page has already seen everything below, off <c>?since=N</c>. Anything that is not
    /// a number is nothing asked for: a feed from the start is the right answer to a query nobody meant.
    /// </summary>
    private static int Since(string? query)
    {
        var since = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)["since"];
        return int.TryParse(since, CultureInfo.InvariantCulture, out var sequence) && sequence > 0 ? sequence : 0;
    }

    /// <summary>
    /// Which asking this poll says the page has drawn, off <c>?shown=N</c>, or null for an ordinary poll.
    /// </summary>
    /// <remarks>
    /// It names the asking and not merely the seat, because a seat is asked several questions in a row and
    /// the host must not start a clock on the second one while it is still serving it. A poll that fetched a
    /// question cannot also be the acknowledgement that it was drawn: that answer still has to reach a phone
    /// and be rendered, and on a slow link the difference is the network, not the player.
    /// </remarks>
    private static long? Shown(string? query) =>
        long.TryParse(System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)["shown"], CultureInfo.InvariantCulture, out var asked)
            ? asked
            : null;

    private async Task<StudioResponse> SeatAsync(TableSeat seat, int since, long? shown)
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
        // Through the session when there is one: closing hands the trace over and the recorder forgets the
        // match, while these two are still reading the end of it on their screens.
        var examined = run is { } recording ? recording.Entries(session.MatchId, since) : events.EntriesOf(session.MatchId, since);

        // The question the seat is blocked on, rather than what the sub-phase allows: a person acts when their
        // own seat is asked, and the two differ while the other seat is still deciding.
        var waiting = seat.Person?.Waiting;

        // The moment this seat was shown what it is being asked, which is what a decision's duration is
        // measured from. Only the page's own acknowledgement of *this* asking counts. Anything else leaves the
        // stamp exactly as it found it: a poll of the other seat from behind the pass screen would time the
        // handover, and the poll that fetches a question is too early to be the moment it was read.
        if (shown is { } drawn && waiting is { } asking && drawn == asking.Asked)
        {
            run?.Served(seat.Slot, asking);
        }

        return StudioResponse.OfJson(
            new
            {
                seat = seat.Name,
                board = board.Value,
                options = options.Value,
                waitingFor = waiting?.Kind.ToString(),
                waitingCreature = waiting?.Creature,

                // Which asking this is. The page sends it back once it has drawn it, and that is what starts
                // the clock: two questions of the same shape in a row are two askings, and the second must not
                // inherit the first's moment.
                waitingAsked = waiting?.Asked,
                playedByBot = seat.Person is null,
                over = session.IsOver,

                // Whether anything a player writes will be kept. A table told --no-record keeps nothing, and
                // the page has to know: buttons that post a note into a 409 are worse than no buttons, because
                // a player who taps one believes the thing they noticed was written down.
                recording = run is not null,
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
            return await RefuseAsync(seat.Slot, check.Error);
        }

        // The question this answers has to be the question the seat is on. `Question.Answers` compares the kind
        // and the creature, which two consecutive Evolution picks share, so without an asking a decision
        // validated against the first could be applied to the second. Read here to fail early and to have
        // something to record, but *checked* inside the seat, under the lock that hands the answer over: here
        // it would be a check and then a gap, and two callers could both pass it before either submitted.
        var waiting = person.Waiting;
        if (posted.Asked is not { } asking)
        {
            return await RefuseAsync(seat.Slot, Late);
        }

        // Where this decision is being made, taken before it is handed over. Submitting releases the driver,
        // which moves the round on, so anything read afterwards is where the match *went*: the step in
        // steps.jsonl carries where it was, and the two files this session says are aligned would disagree
        // about where every decision happened. The sub-phase is the one the options themselves name -- these
        // are the options the decision was just validated against, so it is the question's own answer to
        // "where", not a second reading that could have moved. The round comes off the board, before the tap.
        var subPhase = options.Value.SubPhase;
        var round = (await WhereAsync(seat.Slot)).Round;
        var answered = waiting;

        // And when this seat's screen was served, read now rather than after the submit. Submitting releases
        // the driver, which can ask this seat the next question and have a poll stamp it before the recording
        // gets here -- and a seat has one stamp, so the new question's replaces the answered one's. Read
        // afterwards, the decision would be timed at nothing.
        var servedAt = run?.ServedAt(seat.Slot, answered);


        // How far the trace has got before this decision is handed over, so the checkpoint that follows can
        // tell whether the driver has applied it yet.
        var traced = run?.TraceLength(session.MatchId) ?? 0;

        // Declared before the decision is handed over, and held until its note is written. The match can end
        // on this very tap, and the host would otherwise close the session while this thread is still on its
        // way to writing it down -- showing a finished session whose last decision is missing.
        using var accepting = run?.Accepting();

        // Checked against the options, and still refused: the seat moved on between the two. That is the race
        // the driver would have thrown on, answered as the late tap it is.
        // The moment of acceptance, read *before* the seat is answered and kept only if it was. After the
        // submit is too late: it releases the driver, and this thread can lose the processor between the two,
        // so the clock would then read once a whole command had run -- dating the note after the thing it
        // records and putting engine time inside a duration that measures a person. Read first, it is at worst
        // a few instructions early, and it cannot contain any of the engine, which is the error worth having.
        var acceptedAt = run?.Now();
        if (!person.Submit(decision, asking))
        {
            return await RefuseAsync(seat.Slot, Late);
        }

        var accepted = acceptedAt is { } moment ? new AcceptedDecision(answered, servedAt, moment) : null;

        await RecordAsync(seat.Slot, round, subPhase, accepted, traced);
        return new StudioResponse(204, StudioResponse.Plain, []);
    }

    /// <summary>
    /// Writes the decision down, and never changes the answer. The decision has landed by the time this runs
    /// and cannot be taken back, so a session that fails to record it must not become a 500: the page would
    /// show the seat a failure for a decision the aggregate accepted, and the next tap on a board that has
    /// moved on would be refused as late. A lost line goes to the console, where it is the operator's problem
    /// rather than the player's.
    /// </summary>
    private async Task RecordAsync(PlayerSlot slot, int? round, RoundSubPhase? subPhase, AcceptedDecision? accepted, int traced)
    {
        if (run is not { } recording || accepted is null)
        {
            return;
        }

        try
        {
            await recording.DecidedAsync(session.MatchId, slot, round, subPhase, accepted, CancellationToken.None);

            // The trace, as far as the match has got -- and not until the command this decision caused has
            // finished raising everything it raises.
            await SettledAsync(recording, slot, traced);
            await recording.CheckpointAsync(session.MatchId, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            await Console.Error.WriteLineAsync($"The session could not record a decision of {TableSeat.NameOf(slot)}: {exception.Message}");
        }
    }

    private static readonly DomainError Late = new("Seat.NotWaiting", "This seat is not waiting for that decision any more.");

    /// <summary>
    /// A refusal, as the page reads it and as the session records it. Both from one place: a refusal a player
    /// provoked is exactly what a playtest wants to know about, and one that reached the screen without
    /// reaching <c>notes.jsonl</c> would be a rule that confused somebody and left no trace.
    /// </summary>
    private async Task<StudioResponse> RefuseAsync(PlayerSlot slot, DomainError error)
    {
        if (run is { } recording)
        {
            var (round, subPhase) = await WhereAsync(slot);
            await recording.RefusedAsync(session.MatchId, slot, round, subPhase, error, CancellationToken.None);
        }

        return StudioResponse.OfJson(new { error = error.Code, message = error.Message }, ArtifactJson.LineOptions, status: 409);
    }

    /// <summary>
    /// A note a player wrote, against the round and sub-phase they wrote it in. It is recorded for the seat
    /// whose token posted it, so nobody can file a misplay against the other player.
    /// </summary>
    private async Task<StudioResponse> NoteAsync(TableSeat seat, string body)
    {
        if (run is not { } recording)
        {
            return StudioResponse.OfPlainText(409, "This table is not recording a session, so it has nowhere to keep a note.");
        }

        TableNoteBody? posted;
        try
        {
            posted = JsonSerializer.Deserialize<TableNoteBody>(body, ArtifactJson.LineOptions);
        }
        catch (JsonException exception)
        {
            return StudioResponse.OfPlainText(400, exception.Message);
        }

        if (posted is null)
        {
            return StudioResponse.OfPlainText(400, $"A note names one of: {TableNoteBody.Kinds}.");
        }

        if (posted.ToKind(out var problem) is not { } kind)
        {
            return StudioResponse.OfPlainText(400, problem);
        }

        var (round, subPhase) = await WhereAsync(seat.Slot);
        await recording.TypedAsync(session.MatchId, seat.Slot, round, subPhase, kind, posted.Trimmed(), CancellationToken.None);
        return new StudioResponse(204, StudioResponse.Plain, []);
    }

    /// <summary>
    /// Waits until the engine has finished the command this decision caused, so what is checkpointed next is
    /// the whole of it and not the first event of it.
    /// </summary>
    /// <remarks>
    /// Two things in sequence, and neither is enough alone. The trace growing says the command has *started*:
    /// submitting only queues the driver, so until then this thread is ahead of it. Then a read through the
    /// table's gate says it has *finished* -- the driver holds that gate for a whole command, saving and
    /// dispatching inside it (<c>MatchWorkflow.CommitAsync</c>), so a read that comes back is that command
    /// having let go. One command raises several events, and watching the trace alone would stop at the first
    /// of them and checkpoint while the rest were still being dispatched. The gate is the completion signal
    /// the host would otherwise have had to invent, and it was already there.
    /// </remarks>
    private async Task SettledAsync(PlaytestRun recording, PlayerSlot slot, int traced)
    {
        await recording.WaitForTraceAsync(session.MatchId, traced, CancellationToken.None);
        await WhereAsync(slot);
    }

    /// <summary>
    /// Where in the match a note is about to be written, read off the seat's own board. A board that cannot be
    /// read is not worth refusing a note over: the note is the record, and a missing round number is a smaller
    /// loss than no note at all.
    /// </summary>
    /// <summary>
    /// Changes who plays a seat, from the top of a round the match has not reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything a pilot does goes through the seats and the engine's own commands: this seats a different
    /// agent and nothing else. It writes to no Creature and no Round, which the Domain already makes
    /// unreachable, and the rule is stated here so nobody widens it later "just for the pilot" — a board set
    /// by hand reads as a legal sequence of moves for ever after (<c>docs/tabletop/app-roadmap.md</c>,
    /// stage 6).
    /// </para>
    /// <para>
    /// A round is named rather than an instant, and one the match has not reached. The driver asks a seat for
    /// several decisions inside one sub-phase — a speed for each creature, then an intent for each — so a
    /// seat changing hands between two of them would split that sub-phase between two players and make the
    /// round unreadable. Naming a future round makes that impossible rather than unlikely: the seat holds the
    /// swap and applies it against the board, so it lands where a round begins however long the request took.
    /// </para>
    /// </remarks>
    private async Task<StudioResponse> PilotAsync(string method, string rest, string body, string? token)
    {
        if (pilot is not { } flying)
        {
            return StudioResponse.OfPlainText(404, "This table has no pilot.");
        }

        if (string.IsNullOrWhiteSpace(token) || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token), System.Text.Encoding.UTF8.GetBytes(flying.Token)))
        {
            return StudioResponse.OfPlainText(403, $"Piloting carries the pilot's own '{TokenHeader}', which is not a seat's.");
        }

        if (method != "POST" || TableSeat.SlotOf(rest.TrimEnd('/')) is not { } slot)
        {
            return StudioResponse.OfPlainText(404, $"No such route: {method} {PilotPrefix}{rest}");
        }

        TableSwapBody? posted;
        try
        {
            posted = JsonSerializer.Deserialize<TableSwapBody>(body, ArtifactJson.LineOptions);
        }
        catch (JsonException exception)
        {
            return StudioResponse.OfPlainText(400, exception.Message);
        }

        if (posted?.Agent is not { Length: > 0 } wanted || posted.Round is not { } round)
        {
            return StudioResponse.OfPlainText(400, "A swap names the agent taking the seat and the round it takes it from.");
        }

        // Cheap early out only. The real one is below: resolving a file-backed agent takes long enough for a
        // bot match to finish underneath it, and a swap accepted for a match that has ended is a 200 for
        // something that can never happen.
        if (session.IsOver)
        {
            return Refused(NoRoundsLeft);
        }

        if (flying.Seating(slot, wanted) is not { } next)
        {
            return Refused(new DomainError(NoSuchAgent, $"Nobody called '{wanted}' can sit in {TableSeat.NameOf(slot)}."));
        }

        // Nothing about the seat is read here. The round it has reached, who is sitting in it and whether the
        // swap is taken are one answer from one locked step inside the seat: read separately, a pending swap
        // landing in between would name the wrong occupant as the one replaced, against a round the match had
        // already left.
        var seat = session.Seat(slot);
        var outcome = seat.SwapAt(next, round);
        if (!outcome.Taken)
        {
            var floor = outcome.Reached is { } reached ? reached + 1 : 1;
            return Refused(new DomainError(
                MidRound,
                outcome.Reached is { } playing
                    ? $"Round {playing} is being played; a seat changes hands at the top of a round, so name {floor} or later."
                    : "A seat changes hands at the top of a round, so name round 1 or later."));
        }

        // Checked again, now that the seat has taken it. A match that ended while the agent was being resolved
        // leaves this swap installed and inert -- nothing will ask that seat again -- and the operator is told
        // the truth rather than a 200 for a seat that will never change.
        if (session.IsOver)
        {
            return Refused(NoRoundsLeft);
        }

        var held = outcome.Held.Name;
        await NoteSwapAsync(slot, outcome.Reached, new SeatChange(held, next.Name, round));
        return StudioResponse.OfJson(new { slot = TableSeat.NameOf(slot), from = held, to = next.Name, round }, ArtifactJson.LineOptions);
    }

    /// <summary>
    /// Writes the swap down, and never takes it back. The seat has taken it by the time this runs, so a
    /// session that fails to record it must not become a 500: the operator would be told their request failed
    /// while the seat still changes hands, and a retry would replace a pending swap they think they never
    /// made. A lost line goes to the console, where it is the operator's problem rather than the match's.
    /// </summary>
    private async Task NoteSwapAsync(PlayerSlot slot, int? round, SeatChange change)
    {
        if (run is not { } recording)
        {
            return;
        }

        try
        {
            // The round is the seat's own, read as the swap was taken. A board read here would be after the
            // fact: the driver can enter the round this names before it answers, and the note would then say
            // it was asked for at or after the round it lands at. There is no sub-phase, because a swap is not
            // made at one -- it is an operator's action against a round.
            await recording.SeatedAsync(session.MatchId, slot, round, change, CancellationToken.None);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"  The seat change {change.From} -> {change.To} at round {change.AtRound} could not be written down: {failure.Message}");
        }
    }

    private static StudioResponse Refused(DomainError error) =>
        StudioResponse.OfJson(new { error = error.Code, message = error.Message }, ArtifactJson.LineOptions, status: 409);

    private const string MidRound = "Table.SwapMidRound";

    private const string NoSuchAgent = "Table.NoSuchAgent";

    private static readonly DomainError NoRoundsLeft = new("Table.MatchOver", "This match has no round left to change a seat for.");

    private async Task<(int? Round, RoundSubPhase? SubPhase)> WhereAsync(PlayerSlot slot)
    {
        var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, slot));
        return board.IsSuccess ? (board.Value.RoundNumber, board.Value.SubPhase) : (null, null);
    }
}
