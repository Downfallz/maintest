using System.Text;
using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.Infrastructure.Resources.Authoring;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Piloting: changing who plays a seat while the match runs. Everything it does goes through a seat and the
/// engine's own commands, so what these assert is the boundary around it — which round a swap may name, which
/// token may ask for one, and that the record says a seat changed hands.
/// </summary>
public sealed class TablePilotTests : IDisposable
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 12, 2.0);
    private const string PilotToken = "token-of-the-pilot";
    private const string SeatToken = "token-of-player-1";

    private readonly StudioContent _content = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-pilot-{Guid.NewGuid():N}");
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
    /// The swap a pilot is for: a seat named a later round, and the match carries on without noticing.
    /// </summary>
    [Fact]
    public async Task A_seat_named_a_later_round_changes_hands_and_the_match_keeps_running()
    {
        var table = await Started();

        var answer = await Swap(table, "player1", agent: "random", round: 3);

        answer.Status.ShouldBe(200, Text(answer));
        var said = JsonDocument.Parse(Text(answer)).RootElement;
        said.GetProperty("from").GetString().ShouldBe("Greedy");
        said.GetProperty("to").GetString().ShouldBe("Random");
        said.GetProperty("round").GetInt32().ShouldBe(3);

        var outcome = await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        outcome.IsSuccess.ShouldBeTrue(outcome.IsFailure ? outcome.Error.Message : null);
    }

    /// <summary>
    /// A swap naming the round being played is refused, and refused by name so a page can say why.
    /// </summary>
    /// <remarks>
    /// The driver asks one seat for several decisions inside one sub-phase — a speed for each creature, then
    /// an intent for each — so a seat changing hands between two of them splits that sub-phase between two
    /// players. Round 1 is always under way by the time a table answers anything, which is what makes this
    /// the mid-round case rather than a contrived one.
    /// </remarks>
    [Fact]
    public async Task A_swap_naming_the_round_being_played_is_refused_as_mid_round()
    {
        var table = await Started();

        var answer = await Swap(table, "player1", agent: "random", round: 1);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Table.SwapMidRound");
    }

    /// <summary>A round already played is the same request arriving late, and is refused the same way.</summary>
    [Fact]
    public async Task A_swap_naming_a_round_already_gone_is_refused_too()
    {
        var table = await Started();

        var answer = await Swap(table, "player1", agent: "random", round: 0);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Table.SwapMidRound");
    }

    /// <summary>
    /// A seat's own token cannot pilot. In hotseat both seat tokens sit in one browser beside the page a
    /// player is looking at, so a seat token that could also pilot would let either player hand the other's
    /// seat to a bot.
    /// </summary>
    [Fact]
    public async Task A_seat_token_cannot_change_who_plays_a_seat()
    {
        var table = await Started();

        var answer = await table.Api.HandleAsync("POST", "/api/pilot/seats/player1", """{"agent":"random","round":3}""", SeatToken);

        answer.Status.ShouldBe(403, Text(answer));
    }

    /// <summary>And the pilot's token is not a seat's: it may move seats, never read or answer for one.</summary>
    [Fact]
    public async Task The_pilot_token_cannot_read_or_answer_for_a_seat()
    {
        var table = await Started();

        var answer = await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, PilotToken);

        answer.Status.ShouldBe(403, Text(answer));
    }

    /// <summary>A name nothing can be built from is the operator's typo, not a broken host.</summary>
    [Fact]
    public async Task A_swap_to_nobody_of_that_name_is_refused()
    {
        var table = await Started();

        var answer = await Swap(table, "player1", agent: "clairvoyant", round: 3);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Table.NoSuchAgent");
    }

    /// <summary>A seat no person ever joined has nobody to hand back to, and says so.</summary>
    [Fact]
    public async Task A_seat_with_no_person_cannot_be_handed_to_one()
    {
        var table = await Started();

        var answer = await Swap(table, "player2", agent: "person", round: 3);

        answer.Status.ShouldBe(409, Text(answer));
        JsonDocument.Parse(Text(answer)).RootElement.GetProperty("error").GetString().ShouldBe("Table.NoSuchAgent");
    }

    /// <summary>
    /// The session writes the asking down: who held the seat, who is to take it, where the match was, and the
    /// round it lands at.
    /// </summary>
    [Fact]
    public async Task A_swap_is_noted_with_its_direction_and_both_rounds()
    {
        var table = await Started();

        (await Swap(table, "player1", agent: "random", round: 4)).Status.ShouldBe(200);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await table.Run.FinishAsync(table.Session.MatchId, await Board(table.Session), TestContext.Current.CancellationToken);

        var note = Lines(table.Run, "notes.jsonl").Single(line => line.GetProperty("kind").GetString() == "Seat");
        note.GetProperty("slot").GetString().ShouldBe("Player1");
        note.GetProperty("from").GetString().ShouldBe("Greedy");
        note.GetProperty("to").GetString().ShouldBe("Random");
        note.GetProperty("atRound").GetInt32().ShouldBe(4);
        note.GetProperty("round").GetInt32().ShouldBeLessThan(4, "the note is dated where the match was when it was asked for");
    }

    /// <summary>
    /// The run stamp grows when the seat actually changes hands, so <c>compare-stamps</c> sees a seat that was
    /// played by two agents as what it is.
    /// </summary>
    /// <remarks>
    /// Written from the landing and never from the asking: a swap names a round the match has not reached, so
    /// a stamp built when it was asked for would name a player who never played if the match ended first.
    /// </remarks>
    [Fact]
    public async Task The_run_stamp_grows_when_a_seat_actually_changes_hands()
    {
        var table = await Started();

        (await Swap(table, "player1", agent: "random", round: 3)).Status.ShouldBe(200);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await table.Run.FinishAsync(table.Session.MatchId, await Board(table.Session), TestContext.Current.CancellationToken);

        var stamp = Read(table.Run, "manifest.json").GetProperty("stamp");
        stamp.GetProperty("player1Agent").GetString().ShouldBe("Greedy>Random@3");
        stamp.GetProperty("player2Agent").GetString().ShouldBe("Greedy", "the other seat never changed hands");
    }

    /// <summary>
    /// A swap the match never reaches leaves the stamp alone, which is why the stamp is written from the
    /// landing and not from the asking.
    /// </summary>
    /// <remarks>
    /// This is the test that tells the two designs apart. Both say <c>Greedy&gt;Random@3</c> of a swap that
    /// lands; only one says <c>Greedy</c> of a swap that was asked for and never happened. A stamp naming a
    /// player who never played is the exact thing <c>compare-stamps</c> would read to decide whether two runs
    /// are comparable.
    /// </remarks>
    [Fact]
    public async Task A_swap_the_match_never_reaches_leaves_the_stamp_alone()
    {
        var table = await Started();

        // Past the round cap these rules set, so the match ends before the seat would change hands.
        (await Swap(table, "player1", agent: "random", round: 99)).Status.ShouldBe(200);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await table.Run.FinishAsync(table.Session.MatchId, await Board(table.Session), TestContext.Current.CancellationToken);

        var stamp = Read(table.Run, "manifest.json").GetProperty("stamp");
        stamp.GetProperty("player1Agent").GetString().ShouldBe("Greedy");

        // The asking is still on the record, where an operator's action belongs.
        Lines(table.Run, "notes.jsonl").ShouldContain(line => line.GetProperty("kind").GetString() == "Seat");
    }

    private static Task<StudioResponse> Swap(Table table, string slot, string agent, int round) =>
        table.Api.HandleAsync("POST", $"/api/pilot/seats/{slot}", $$"""{"agent":"{{agent}}","round":{{round}}}""", PilotToken);

    private static IReadOnlyList<JsonElement> Lines(PlaytestRun run, string relativePath) =>
        [.. File.ReadAllLines(Path.Combine(run.Directory, relativePath))
            .Where(line => line.Length > 0)
            .Select(line => JsonDocument.Parse(line).RootElement)];

    private static JsonElement Read(PlaytestRun run, string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Directory, relativePath))).RootElement;

    private static async Task<PlayerBoardState> Board(TableSession session)
    {
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, PlayerSlot.Player1));
        return board.Value;
    }

    private static string Text(StudioResponse response) => Encoding.UTF8.GetString(response.Body);

    private async Task<Table> Started()
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);

        var resources = _host.Services.GetRequiredService<IGameResources>();
        var run = PlaytestRun.Open(
            _runs,
            new PlaytestSetup(resources, Rules, Seed: 7, "Greedy", "Greedy"),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            TimeProvider.System);
        await run.StartAsync(CatalogueProjection.Build(resources, Rules), _stopping.Token);

        // Two bots, so the match plays itself to an outcome while the pilot moves a seat under it. What is
        // being tested is the piloting, and a person in a seat would only add a thread to block on. Player 1's
        // agent says when it has first been asked something, because "a round is being played" is only true of
        // a seat that has been asked about it, and a test that posted before that would be testing the other
        // case: a seat nothing has begun for, which may be seated for any round at all.
        var asked = new Watching(new GreedyAgent(resources, Rules));
        var session = await TableSession.StartAsync(
            _host.Services,
            Rules,
            seed: 7,
            new SeatAgent(new Occupant(asked, "Greedy")),
            new SeatAgent(new Occupant(new GreedyAgent(resources, Rules), "Greedy")),
            run.Wrap,
            _stopping.Token);

        var pilot = new TablePilot(PilotToken, (slot, wanted) => wanted switch
        {
            "random" => new Occupant(new RandomAgent(new SeededRandomSource(3)), "Random"),
            "person" when slot == PlayerSlot.Player1 => new Occupant(new GreedyAgent(resources, Rules), "human:mk"),
            _ => null,
        });

        var api = new TableApi(
            session,
            session.Queries,
            [new TableSeat(PlayerSlot.Player1, SeatToken, Person: null), new TableSeat(PlayerSlot.Player2, "token-of-player-2", Person: null)],
            CatalogueProjection.Build(resources, Rules),
            _host.Services.GetRequiredService<MatchTraceRecorder>(),
            run,
            pilot);

        asked.Asked.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue("player 1 should have been asked something");
        return new Table(api, session, run);
    }

    private sealed record Table(TableApi Api, TableSession Session, PlaytestRun Run);

    /// <summary>A bot that says when it has first been asked for a decision, and plays as itself otherwise.</summary>
    private sealed class Watching(IPlayerAgent inner) : IPlayerAgent
    {
        public ManualResetEvent Asked { get; } = new(false);

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
        {
            Asked.Set();
            return inner.DecideEvolution(board, options);
        }

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            Asked.Set();
            return inner.DecideSpeed(board, creature);
        }

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            Asked.Set();
            return inner.DecideIntent(board, intentOption);
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
        {
            Asked.Set();
            return inner.DecideTargets(board, options);
        }
    }
}
