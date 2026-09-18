using System.Text;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Authoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// <c>notes.jsonl</c>, which is the one thing a playtest knows that no other artifact carries. What matters
/// is that the host writes the two kinds it owns and refuses to let a client write them, and that a duration
/// is measured from the moment a screen was served rather than off the wall clock.
/// </summary>
public sealed class PlaytestNotesTests : IDisposable
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 6, 2.0);
    private static readonly DateTimeOffset Start = new(2026, 9, 17, 20, 30, 0, TimeSpan.Zero);

    private readonly StudioContent _content = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly SteppingClock _clock = new(Start);
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-notes-{Guid.NewGuid():N}");
    private IHost? _host;

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _host?.Dispose();
        _content.Dispose();
        if (Directory.Exists(_runs))
        {
            Directory.Delete(_runs, recursive: true);
        }
    }

    /// <summary>
    /// The duration is served-to-accepted. The clock moves between the two here, so a note that read the wall
    /// clock instead would report something near zero and fail.
    /// </summary>
    [Fact]
    public async Task A_decision_is_noted_with_where_it_happened_and_how_long_it_took()
    {
        var table = await Recording();

        await Shown(table);
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        await Post(table, """{"kind":"Evolution","pass":true}""");

        var note = Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision");
        note.GetProperty("elapsedMs").GetInt64().ShouldBe(1500);
        note.GetProperty("slot").GetString().ShouldBe("Player1");
        note.GetProperty("subPhase").GetString().ShouldBe("Evolution");
        note.GetProperty("sessionId").GetString().ShouldBe(table.Run.SessionId);
    }

    /// <summary>
    /// The acceptance moment is read before the seat is answered, and so carries none of what answering it
    /// sets off. Handing a decision over releases the driver, which runs a whole command and asks the next
    /// question; a thread that reads its clock afterwards can be scheduled again at any point in that, and
    /// would date the note after the thing it records and put engine time inside a duration that measures a
    /// person reading a screen.
    /// </summary>
    /// <remarks>
    /// Read after the submit, this reports a minute: the clock here moves exactly when the seat comes off the
    /// question being answered, which is what the submit does, under its own lock, before it returns. The
    /// other timing tests in this file cannot catch it -- their clock only moves when the test moves it, so it
    /// reads the same on both sides of the handoff and both orderings pass.
    /// </remarks>
    [Fact]
    public async Task A_decision_is_timed_before_it_is_handed_to_the_engine()
    {
        var clock = new ClockThatJumpsOnceTheSeatIsAnswered(Start, Start.AddMinutes(1));
        var table = await Recording(clock);

        clock.Answering(table.Person);
        await Shown(table);
        await Post(table, """{"kind":"Evolution","pass":true}""");

        var note = Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision");
        note.GetProperty("at").GetDateTimeOffset().ShouldBe(Start);
        note.GetProperty("elapsedMs").GetInt64().ShouldBe(0);
    }

    /// <summary>
    /// In hotseat the page polls both seats on a timer while one of them is behind the pass screen, so a poll
    /// that is not drawing the seat must not start its clock: the duration would include the walk round the
    /// table, picking the phone up and tapping ready. Here the background polls happen and then the screen is
    /// served, and only the last of the three is what the decision is timed from.
    /// </summary>
    [Fact]
    public async Task A_poll_that_is_not_drawing_the_seat_does_not_start_its_clock()
    {
        var table = await Recording();

        await Get(table);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await Get(table);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await Shown(table);
        _clock.Advance(TimeSpan.FromMilliseconds(400));
        await Post(table, """{"kind":"Evolution","pass":true}""");

        Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision")
            .GetProperty("elapsedMs").GetInt64().ShouldBe(400);
    }

    /// <summary>A person reading the same question while the page polls keeps the moment it was first shown.</summary>
    [Fact]
    public async Task Polling_the_same_question_again_keeps_the_moment_it_was_first_shown()
    {
        var table = await Recording();

        await Shown(table);
        _clock.Advance(TimeSpan.FromMilliseconds(700));
        await Shown(table);
        _clock.Advance(TimeSpan.FromMilliseconds(700));
        await Shown(table);
        await Post(table, """{"kind":"Evolution","pass":true}""");

        Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision")
            .GetProperty("elapsedMs").GetInt64().ShouldBe(1400);
    }

    /// <summary>
    /// A decision is noted where it was asked, not where the match went once it was accepted. The sub-phase is
    /// the one the options the decision was validated against named, which is why this holds by construction
    /// rather than by winning a race: submitting releases the driver, and anything read afterwards may already
    /// be the next sub-phase. There is deliberately no test here for that race -- with a bot in the other seat
    /// the driver advances in microseconds, so a test that asserted on it would pass or fail by timing, which
    /// is worse than no test. What is asserted is the contract: the note says what the options said.
    /// </summary>
    [Fact]
    public async Task A_decision_is_noted_in_the_sub_phase_its_options_named()
    {
        var table = await Recording();
        var served = await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token, query: "?shown=1");
        var asked = JsonDocument.Parse(Text(served)).RootElement.GetProperty("options").GetProperty("subPhase").GetString();

        await Post(table, """{"kind":"Evolution","pass":true}""");

        asked.ShouldBe("Evolution");
        Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision")
            .GetProperty("subPhase").GetString().ShouldBe(asked);
    }

    /// <summary>
    /// A decision posted without the page ever saying the board was up -- a scripted seat here, a tap that beat
    /// its own visibility request at a table. The duration is unknown, and unknown is written rather than zero:
    /// zero reads like an instant decision and would quietly pollute the one measurement this file exists for.
    /// </summary>
    [Fact]
    public async Task A_decision_whose_screen_was_never_announced_is_noted_with_no_duration()
    {
        var table = await Recording();

        await Post(table, """{"kind":"Evolution","pass":true}""");

        Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision")
            .GetProperty("elapsedMs").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// An acknowledgement of a different asking is not this one's. One seat is asked several questions in a
    /// row, so a page that named the wrong one -- or a stale one still in flight -- must not start this
    /// question's clock, or the duration begins before anybody saw it.
    /// </summary>
    [Fact]
    public async Task An_acknowledgement_of_another_asking_does_not_start_this_ones_clock()
    {
        var table = await Recording();

        await Get(table, $"?shown={(table.Person.Waiting?.Asked ?? 0) + 99}");
        _clock.Advance(TimeSpan.FromSeconds(30));
        await Post(table, """{"kind":"Evolution","pass":true}""");

        Notes(table).Single(note => note.GetProperty("kind").GetString() == "Decision")
            .GetProperty("elapsedMs").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The page is told which asking it is drawing, or it cannot acknowledge one.</summary>
    [Fact]
    public async Task A_seat_is_served_the_asking_it_is_waiting_on()
    {
        var table = await Recording();

        var served = await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token);

        JsonDocument.Parse(Text(served)).RootElement.GetProperty("waitingAsked").GetInt64()
            .ShouldBe(table.Person.Waiting!.Asked);
    }

    /// <summary>A rule that confused somebody and left no trace is the failure this prevents.</summary>
    [Fact]
    public async Task A_refused_decision_is_noted_with_the_error_that_refused_it()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", """{"kind":"Speed","creature":1,"speed":"Quick"}""", table.Token);

        answer.Status.ShouldBe(409, Text(answer));
        var note = Notes(table).Single(note => note.GetProperty("kind").GetString() == "Refused");
        note.GetProperty("code").GetString().ShouldNotBeNullOrWhiteSpace();
        note.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        note.GetProperty("elapsedMs").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>One tap, so a lookup carries no text and is still a note.</summary>
    [Fact]
    public async Task A_note_a_player_taps_needs_no_text()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/notes", """{"kind":"Lookup"}""", table.Token);

        answer.Status.ShouldBe(204, Text(answer));
        var note = Notes(table).Single();
        note.GetProperty("kind").GetString().ShouldBe("Lookup");
        note.GetProperty("text").GetString().ShouldBe(string.Empty);
        note.GetProperty("round").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task A_comment_carries_what_the_player_wrote()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/notes", """{"kind":"Comment","text":"  the reveal order confused us  "}""", table.Token);

        answer.Status.ShouldBe(204, Text(answer));
        Notes(table).Single().GetProperty("text").GetString().ShouldBe("the reveal order confused us");
    }

    [Fact]
    public async Task A_comment_with_nothing_in_it_is_nothing_to_record()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/notes", """{"kind":"Comment","text":"   "}""", table.Token);

        answer.Status.ShouldBe(400);
        Notes(table).ShouldBeEmpty();
    }

    /// <summary>
    /// The host's own account of what happened is not something a client may write: a page that could post a
    /// Decision could rewrite how long a player took, which is the one thing this file is for.
    /// </summary>
    [Theory]
    [InlineData("Decision")]
    [InlineData("Refused")]
    [InlineData("Nonsense")]
    public async Task A_kind_a_player_does_not_write_is_refused(string kind)
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/notes", $$"""{"kind":"{{kind}}"}""", table.Token);

        answer.Status.ShouldBe(400);
        Notes(table).ShouldBeEmpty();
    }

    /// <summary>Nobody files a misplay against the other player: the note is the poster's seat, not a field.</summary>
    [Fact]
    public async Task A_note_is_recorded_for_the_seat_whose_token_posted_it()
    {
        var table = await Recording();

        await table.Api.HandleAsync("POST", "/api/notes", """{"kind":"Misplay"}""", "token-of-player-2");

        Notes(table).Single().GetProperty("slot").GetString().ShouldBe("Player2");
    }

    [Fact]
    public async Task A_note_needs_a_token_like_every_other_request()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("POST", "/api/notes", """{"kind":"Lookup"}""", token: null);

        answer.Status.ShouldBe(403);
    }

    [Fact]
    public async Task A_note_is_the_only_thing_that_route_takes()
    {
        var table = await Recording();

        var answer = await table.Api.HandleAsync("GET", "/api/notes", string.Empty, table.Token);

        answer.Status.ShouldBe(404);
    }

    private static IEnumerable<JsonElement> Notes((TableApi Api, TableSession Session, HumanSeat Person, string Token, PlaytestRun Run) table) =>
        File.ReadAllLines(Path.Combine(table.Run.Directory, "notes.jsonl"))
            .Where(line => line.Length > 0)
            .Select(line => JsonDocument.Parse(line).RootElement);

    /// <summary>
    /// The page saying it has drawn what this seat is being asked, which is what the host times a decision
    /// from. It names the asking, so acknowledging one question never starts another's clock.
    /// </summary>
    private static Task Shown((TableApi Api, TableSession Session, HumanSeat Person, string Token, PlaytestRun Run) table) =>
        Get(table, $"?shown={table.Person.Waiting?.Asked}");

    private static async Task Get((TableApi Api, TableSession Session, HumanSeat Person, string Token, PlaytestRun Run) table, string query = "")
    {
        var answer = await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token, query: query);
        answer.Status.ShouldBe(200, Text(answer));
    }

    /// <summary>
    /// A decision carries the asking it answers, as the page's does off <c>waitingAsked</c>. The host refuses
    /// one that names another question, so a test that left it out would be testing that refusal.
    /// </summary>
    private static string Answering(string body, HumanSeat person) =>
        body.Insert(1, $"\"asked\":{person.Waiting?.Asked ?? 0},");

    private static async Task Post((TableApi Api, TableSession Session, HumanSeat Person, string Token, PlaytestRun Run) table, string body)
    {
        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", Answering(body, table.Person), table.Token);
        answer.Status.ShouldBe(204, Text(answer));
    }

    private async Task<(TableApi Api, TableSession Session, HumanSeat Person, string Token, PlaytestRun Run)> Recording(TimeProvider? clock = null)
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);

        var resources = _host.Services.GetRequiredService<IGameResources>();
        var person = new HumanSeat(_stopping.Token);
        var bot = new GreedyAgent(resources, Rules);
        var run = PlaytestRun.Open(
            _runs,
            new PlaytestSetup(resources, Rules, Seed: 7, "human:mk", "greedy"),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            clock ?? _clock);
        await run.StartAsync(CatalogueProjection.Build(resources, Rules), _stopping.Token);

        var session = await TableSession.StartAsync(_host.Services, Rules, seed: 7, new SeatAgent(new Occupant(person, "human:mk")), new SeatAgent(new Occupant(bot, "greedy")), run.Wrap, _stopping.Token);
        const string token = "token-of-player-1";
        var api = new TableApi(
            session,
            session.Queries,
            [new TableSeat(PlayerSlot.Player1, token, person), new TableSeat(PlayerSlot.Player2, "token-of-player-2", Person: null)],
            CatalogueProjection.Build(resources, Rules),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            run);

        await Waiting(person, "Evolution");
        return (api, session, person, token, run);
    }

    /// <summary>The seat blocks on another thread, so a test waits for the question rather than assuming it.</summary>
    private static async Task Waiting(HumanSeat person, string kind)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (person.Waiting?.Kind.ToString() == kind)
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"The seat never waited for {kind}; it is waiting for {person.Waiting?.Kind.ToString() ?? "nothing"}.");
    }

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);

    /// <summary>
    /// A clock that stands still while the seat is on one named question and jumps the moment it is off it.
    /// The jump stands for everything that happens once a decision is handed over -- the driver released, a
    /// command run, the next question asked -- so a note holding any of it reads a minute late.
    /// </summary>
    /// <remarks>
    /// The two orderings are told apart with no race because <see cref="HumanSeat.Submit" /> clears the
    /// question under its own lock before it returns, and an asking is never reused: while the seat is on
    /// asking <c>n</c> the read is before the handoff, and once it is on null or on anything past <c>n</c> the
    /// read is after it. Neither branch waits on a thread being scheduled, which is why the bug this catches
    /// can be caught at all -- it is a race in production and an ordering here.
    /// </remarks>
    private sealed class ClockThatJumpsOnceTheSeatIsAnswered(DateTimeOffset pending, DateTimeOffset answered) : TimeProvider
    {
        private HumanSeat? _seat;
        private long? _asking;

        public override DateTimeOffset GetUtcNow() =>
            _seat is { } seat && _asking is { } asking && seat.Waiting?.Asked != asking ? answered : pending;

        /// <summary>Freeze the clock until this seat is off the question it is waiting on right now.</summary>
        public void Answering(HumanSeat seat)
        {
            _seat = seat;
            _asking = seat.Waiting?.Asked;
        }
    }

    /// <summary>A clock a test moves by hand, so a measured duration is the one the test asked for.</summary>
    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
