using System.Text;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
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
/// What a played session leaves on disk. The point of recording a playtest is that it is readable afterwards
/// by everything that already reads a bot run, so these assert the files rather than the calls.
/// </summary>
public sealed class PlaytestRunTests : IDisposable
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 6, 2.0);

    private readonly StudioContent _content = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-playtest-{Guid.NewGuid():N}");
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
    /// The bot run's four files, plus the two only a human session needs. Six, because a session read a year
    /// later has to carry the cards its Spell ids meant and what the players said about them.
    /// </summary>
    [Fact]
    public async Task A_played_session_leaves_the_six_files_it_is_read_from()
    {
        var (run, session) = await Started();
        var outcome = await session.Outcome;
        outcome.IsSuccess.ShouldBeTrue();
        await run.FinishAsync(session.MatchId, await Board(session), TestContext.Current.CancellationToken);

        Directory.GetFiles(run.Directory).Select(Path.GetFileName).ShouldBe(
            ["catalogue.json", "episodes.jsonl", "manifest.json", "notes.jsonl", "steps.jsonl"],
            ignoreOrder: true);
        File.Exists(Path.Combine(run.Directory, "traces", $"{session.MatchId}.json")).ShouldBeTrue();
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
        File.ReadAllText(Path.Combine(run.Directory, "steps.jsonl")).ShouldBeEmpty();
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

    private static async Task<PlayerBoardState> Board(TableSession session)
    {
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, PlayerSlot.Player1));
        board.IsSuccess.ShouldBeTrue();
        return board.Value;
    }

    private static JsonElement Read(PlaytestRun run, string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Directory, relativePath))).RootElement;

    /// <summary>A session that stops at the first question, because a person is holding one of the seats.</summary>
    private async Task<(PlaytestRun Run, TableSession Session, HumanSeat Person)> StartedWithAPerson()
    {
        var person = new HumanSeat(_stopping.Token);
        var (run, session) = await Started(seat1: new SeatAgent(person));
        return (run, session, person);
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

    /// <summary>
    /// A session with a bot in each seat by default, which is the one that plays itself to an outcome without
    /// a tap. What it exercises is the recording, and the recording does not know who is seated.
    /// </summary>
    private async Task<(PlaytestRun Run, TableSession Session)> Started(string player1Agent = "greedy", SeatAgent? seat1 = null)
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);

        var resources = _host.Services.GetRequiredService<IGameResources>();
        var run = PlaytestRun.Open(
            _runs,
            resources,
            Rules,
            seed: 7,
            player1Agent,
            "greedy",
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            TimeProvider.System);
        await run.StartAsync(CatalogueProjection.Build(resources, Rules), _stopping.Token);

        var session = await TableSession.StartAsync(
            _host.Services,
            Rules,
            seed: 7,
            seat1 ?? new SeatAgent(Bot(resources)),
            new SeatAgent(Bot(resources)),
            run.Wrap,
            _stopping.Token);

        return (run, session);
    }

    private static GreedyAgent Bot(IGameResources resources) => new(resources, Rules);
}
