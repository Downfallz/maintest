using System.Text;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Resources.Authoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What a played session leaves on disk. The point of recording a playtest is that it is readable afterwards
/// by everything that already reads a bot run, so these assert the files rather than the calls.
/// </summary>
public sealed class PlaytestRunTests : IDisposable
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 6, 2.0);

    private readonly StudioContent _content = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-playtest-{Guid.NewGuid():N}");
    private readonly List<SteppedSession> _sessions = [];
    private IHost? _host;

    public void Dispose()
    {
        _stopping.Cancel();
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _stopping.Dispose();
        _host?.Dispose();
        _content.Dispose();
        if (Directory.Exists(_runs))
        {
            Directory.Delete(_runs, recursive: true);
        }
    }

    /// <summary>
    /// The bot run's four files, plus the two only a human session needs. Six, because a session read a year
    /// later has to carry the cards its Spell ids meant and what the players said about them.
    /// </summary>
    [Fact]
    public async Task A_played_session_leaves_the_seven_files_it_is_read_from()
    {
        var (run, session) = await Started();
        var outcome = await session.Outcome;
        outcome.IsSuccess.ShouldBeTrue();
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        Directory.GetFiles(run.Location).Select(Path.GetFileName).ShouldBe(
            ["catalogue.json", "decisions.jsonl", "episodes.jsonl", "manifest.json", "notes.jsonl", "steps.jsonl"],
            ignoreOrder: true);
        File.Exists(Path.Combine(run.Location, "traces", $"{session.MatchId}.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task A_finished_session_counts_the_steps_it_recorded()
    {
        var (run, session) = await Started();
        await session.Outcome;
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        var manifest = Read(run, "manifest.json");
        manifest.GetProperty("matches").GetInt32().ShouldBe(1);
        manifest.GetProperty("steps").GetInt32().ShouldBeGreaterThan(0);
        manifest.GetProperty("episodes").GetInt32().ShouldBe(2);
    }

    /// <summary>
    /// A session nobody finished: a person is still being asked something when the table is walked away from.
    /// The trace is readable and shorter than a finished one, and the manifest is the one written when the
    /// session opened -- which says "this was abandoned" out loud, where a manifest counting the steps of an
    /// unfinished match would read exactly like a finished one.
    /// </summary>
    /// <summary>
    /// A checkpoint keeps the match as its journal holds it and no more (ADR 0159): entries past that may be of a
    /// step that is never written, and a host picking the table up holds its rebuilt match against what is kept.
    /// </summary>
    [Fact]
    public async Task A_checkpoint_keeps_the_entries_it_is_told_are_the_match_s_and_no_more()
    {
        var (run, session, person) = await StartedWithAPerson();
        await Waiting(person, "Evolution");

        await run.CheckpointAsync(session.MatchId, upTo: 2, TestContext.Current.CancellationToken);

        Read(run, Path.Combine("traces", $"{session.MatchId}.json")).GetProperty("entries").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task An_abandoned_session_keeps_its_zero_count_manifest_and_a_readable_partial_trace()
    {
        var (run, session, person) = await StartedWithAPerson();
        await Waiting(person, "Evolution");
        await run.CheckpointAsync(session.MatchId, TestContext.Current.CancellationToken);

        var manifest = Read(run, "manifest.json");
        manifest.GetProperty("matches").GetInt32().ShouldBe(0);
        manifest.GetProperty("steps").GetInt32().ShouldBe(0);

        var trace = Read(run, Path.Combine("traces", $"{session.MatchId}.json"));
        trace.GetProperty("entries").GetArrayLength().ShouldBeGreaterThan(0);
        trace.GetProperty("outcome").ValueKind.ShouldBe(JsonValueKind.Null);

        // Every step of an abandoned session is lost: RunRecorder holds them until the match is closed. The
        // notes and the trace survive, the dataset does not, and a reader who finds nine rounds of trace
        // beside an empty steps.jsonl should know that is the design and not a bug.
        File.ReadAllText(Path.Combine(run.Location, "steps.jsonl")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("gained")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("baseRequested")]
    [InlineData("passiveRequested")]
    public async Task A_changed_energy_receipt_refuses_a_checkpoint_even_when_its_boards_agree(string field)
    {
        var (run, session) = await Started();
        await session.Outcome;
        await run.CheckpointAsync(session.MatchId, TestContext.Current.CancellationToken);
        var path = Path.Combine(run.Location, "traces", $"{session.MatchId}.json");
        var trace = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!;
        var receipt = trace["entries"]!.AsArray().First(entry => entry!["event"]!["kind"]!.GetValue<string>() == "RoundEnergyGranted")!["event"]!;
        receipt["grants"]![0]![field] = receipt["grants"]![0]![field]!.GetValue<int>() + 1;
        await File.WriteAllTextAsync(path, trace.ToJsonString(), TestContext.Current.CancellationToken);

        var agrees = await run.AgreesWithCheckpointSoFarAsync(session.MatchId, TestContext.Current.CancellationToken);

        agrees.ShouldBeFalse();
    }

    /// <summary>
    /// Closing a session makes the recorder forget the match, and the host goes on serving because two people
    /// are still reading the end of what they played. The feed would go empty at that moment and never
    /// recover, taking the deciding blow and the outcome off both screens, so the entries survive the closing.
    /// </summary>
    [Fact]
    public async Task The_feed_survives_the_session_being_closed()
    {
        var (run, session) = await Started();
        await session.Outcome;
        var live = run.Entries(session.MatchId, 0).Count;
        live.ShouldBeGreaterThan(0);

        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        _host!.Services.GetRequiredService<MatchTraceRecorder>().EntriesOf(session.MatchId).ShouldBeEmpty("the recorder has handed the match over");
        run.Entries(session.MatchId, 0).Count.ShouldBe(live, "and the session still has it for the page");
        run.Entries(session.MatchId, live - 1).Count.ShouldBe(1, "a page resuming from its cursor gets the tail, not everything");
    }

    /// <summary>The stamp is what tells two sessions apart, and a rule set is half of what makes one reproducible.</summary>
    [Fact]
    public async Task A_session_stamps_the_rule_set_it_played_and_who_played_it()
    {
        var (run, session) = await Started(player1Agent: "human:mk");
        await session.Outcome;
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        var stamp = Read(run, "manifest.json").GetProperty("stamp");
        stamp.GetProperty("player1Agent").GetString().ShouldBe("human:mk");
        stamp.GetProperty("ruleSet").GetProperty("roundCap").GetInt32().ShouldBe(6);
        stamp.GetProperty("ruleSet").GetProperty("teamSize").GetInt32().ShouldBe(2);
    }

    /// <summary>
    /// The catalogue beside the session, because a trace carries Spell ids and a catalogue tuned twenty times
    /// since would read them against cards that no longer exist.
    /// </summary>
    [Fact]
    public async Task A_session_carries_the_catalogue_its_ids_meant()
    {
        var (run, session) = await Started();
        await session.Outcome;

        var catalogue = Read(run, "catalogue.json");
        catalogue.GetProperty("cards").GetArrayLength().ShouldBeGreaterThan(0);
        catalogue.GetProperty("contentHash").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A decision in flight when the match ends: the request thread is parked on a query behind the driver's
    /// own writes, the host closes the session, and only then does the request reach its checkpoint. The
    /// recorder has forgotten the match by then, so a checkpoint that still wrote would replace the real trace
    /// with an empty one, with nothing to say it had.
    /// </summary>
    [Fact]
    public async Task A_checkpoint_after_the_session_closed_cannot_replace_the_trace_it_wrote()
    {
        var (run, session) = await Started();
        await session.Outcome;
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);
        var written = Read(run, Path.Combine("traces", $"{session.MatchId}.json")).GetProperty("entries").GetArrayLength();

        await run.CheckpointAsync(session.MatchId, TestContext.Current.CancellationToken);

        written.ShouldBeGreaterThan(0);
        Read(run, Path.Combine("traces", $"{session.MatchId}.json")).GetProperty("entries").GetArrayLength().ShouldBe(written);
    }

    /// <summary>
    /// A seat that changed hands mid-match says so on every step, so a fast-forwarded session can be split
    /// into the bot's opening and the person's play.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the field, and the lie it replaces was already in the repository: a
    /// <c>--handover</c> session records the bot's rounds and the person's rounds into one
    /// <c>steps.jsonl</c> under one run stamp, with nothing on the line to tell them apart. A clone fitted on
    /// it would learn the bot's opening as human play. Both seats here are bots, because what is being tested
    /// is who the recorder <em>names</em>, and a person in the seat would only add a thread to block on.
    /// </remarks>
    [Fact]
    public async Task A_seat_handed_over_mid_match_names_the_bot_and_the_person_on_their_own_steps()
    {
        const int HandsOverAt = 3;
        var (run, session) = await Started(seat1: resources =>
            new SeatPlan(new Occupant(Bot(resources), "greedy"), new Occupant(Bot(resources), "greedy"))
                .From(PlayerSlot.Player1, HandsOverAt, new Occupant(Bot(resources), "human:mk"), reached: 0));
        await session.Outcome;
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        var mine = Lines(run, "steps.jsonl")
            .Where(step => step.GetProperty("slot").GetString() == "Player1")
            .Select(step => (Round: step.GetProperty("round").GetInt32(), By: step.GetProperty("decidedBy").GetString()))
            .ToList();

        mine.ShouldNotBeEmpty();
        mine.Where(step => step.Round < HandsOverAt).ShouldAllBe(step => step.By == "greedy");
        mine.Where(step => step.Round >= HandsOverAt).ShouldAllBe(step => step.By == "human:mk");
        mine.ShouldContain(step => step.By == "greedy", "the bot played the rounds before the handover");
        mine.ShouldContain(step => step.By == "human:mk", "the person played the rounds after it");

        // The other seat never changed hands, and says the one name it always had.
        Lines(run, "steps.jsonl")
            .Where(step => step.GetProperty("slot").GetString() == "Player2")
            .ShouldAllBe(step => step.GetProperty("decidedBy").GetString() == "greedy");
    }

    /// <summary>
    /// A seat handed over before its very first decision still says so in the stamp.
    /// </summary>
    /// <remarks>
    /// <c>--handover 1</c> is exactly this: the swap lands before the seat has decided anything, so the first
    /// name the session ever observes is already the new occupant's. Taking that first observation as the
    /// baseline — which is what it did — records it as the original, and the manifest then says a bot played a
    /// session every step of which names the person. The baseline is the stamp the manifest was opened with.
    /// </remarks>
    [Fact]
    public async Task A_seat_handed_over_before_its_first_decision_still_grows_the_stamp()
    {
        var (run, session) = await Started(player1Agent: "Greedy", seat1: resources =>
            new SeatPlan(new Occupant(Bot(resources), "Greedy"), new Occupant(Bot(resources), "greedy"))
                .From(PlayerSlot.Player1, 1, new Occupant(Bot(resources), "human:mk"), reached: 0));
        await session.Outcome;
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        Read(run, "manifest.json").GetProperty("stamp").GetProperty("player1Agent").GetString().ShouldBe("Greedy>human:mk@1");
        Lines(run, "steps.jsonl")
            .Where(step => step.GetProperty("slot").GetString() == "Player1")
            .ShouldAllBe(step => step.GetProperty("decidedBy").GetString() == "human:mk");
    }

    private static async Task<PlayerBoardState> Board(SteppedSession session)
    {
        var board = await session.BoardAsync(PlayerSlot.Player1);
        board.IsSuccess.ShouldBeTrue();
        return board.Value;
    }

    /// <summary>A session that stops at the first question, because a person is holding one of the seats.</summary>
    private async Task<(PlaytestRun Run, SteppedSession Session, SteppedPerson Person)> StartedWithAPerson()
    {
        var person = new SteppedPerson();
        var (run, session) = await Started(seat1: resources => new SeatPlan(SeatPlan.Person("human:mk"), new Occupant(Bot(resources), "greedy")), person: person);
        return (run, session, person);
    }

    /// <summary>The bots take their turns behind the table, so a test waits for the question rather than assuming it.</summary>
    private static async Task Waiting(SteppedPerson person, string kind)
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

    /// <summary>
    /// A session with a bot in each seat by default, which is the one that plays itself to an outcome without
    /// a tap, stepped over a journal in the run's directory. What it exercises is the recording, and the
    /// recording does not know who is seated.
    /// </summary>
    /// <param name="seat1">Who sits where, when not two bots; the opening's first seat is what the stamp names.</param>
    /// <param name="person">The first seat's person, when the plan seats one.</param>
    private async Task<(PlaytestRun Run, SteppedSession Session)> Started(
        string player1Agent = "greedy",
        Func<IGameResources, SeatPlan>? seat1 = null,
        SteppedPerson? person = null)
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);

        var resources = _host.Services.GetRequiredService<IGameResources>();
        var run = PlaytestRun.Open(
            new FileArtifactStore(_runs),
            new PlaytestSetup(resources, Rules, Seed: 7, person is null ? player1Agent : "human:mk", "greedy"),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            TimeProvider.System);
        await run.StartAsync(CatalogueProjection.Build(resources, Rules), _stopping.Token);

        var plan = seat1?.Invoke(resources) ?? new SeatPlan(new Occupant(Bot(resources), player1Agent), new Occupant(Bot(resources), "greedy"));
        TableSeat[] seats = [new TableSeat(PlayerSlot.Player1, "seat-1", person), new TableSeat(PlayerSlot.Player2, "seat-2", Person: null)];
        var session = await Stepped.OpenAsync(
            _host.Services,
            Rules,
            seed: 7,
            new SteppedSeats(plan, seats, static (_, _) => null),
            _stopping.Token,
            new FileDecisionJournal(_runs, TimeProvider.System),
            run);
        _sessions.Add(session);
        session.Begin();
        return (run, session);
    }

    private static GreedyAgent Bot(IGameResources resources) => new(resources, Rules);

    private static IReadOnlyList<JsonElement> Lines(PlaytestRun run, string relativePath) =>
        [.. File.ReadAllLines(Path.Combine(run.Location, relativePath))
            .Where(line => line.Length > 0)
            .Select(line => JsonDocument.Parse(line).RootElement)];

    private static JsonElement Read(PlaytestRun run, string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Location, relativePath))).RootElement;

}
