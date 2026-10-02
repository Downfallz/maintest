using System.Text.Json;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A host that starts over a store rebuilds the tables the host before it left unfinished (ADR 0091): same
/// board, same question, same links and codes. Two hosts over one store stand in for the one before and the
/// one after a redeploy; the first is never disposed before the second has rebuilt, because a replica that
/// went away did not close anything.
/// </summary>
public sealed class TableRestorerTests : IDisposable
{
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-restore-{Guid.NewGuid():N}");
    private readonly HostedTables _before;
    private readonly HostedTables _after;
    private readonly TableRegistry _earlier = new();
    private readonly TableRegistry _later = new();

    public TableRestorerTests()
    {
        _before = new HostedTables(_ => new FileArtifactStore(_runs));
        _after = new HostedTables(_ => new FileArtifactStore(_runs));
    }

    public void Dispose()
    {
        _later.Dispose();
        _earlier.Dispose();
        _after.Dispose();
        _before.Dispose();
        if (Directory.Exists(_runs))
        {
            Directory.Delete(_runs, recursive: true);
        }
    }

    [Fact]
    public async Task A_table_a_person_was_playing_is_rebuilt_at_the_same_question_with_the_same_links_and_codes()
    {
        var table = await Opened(HostedTables.OnePerson);
        var person = table.Seats[0].Person!;
        await PlayUntilIntent(table, person);
        var asked = person.Waiting.ShouldNotBeNull();
        var board = await Board(table.Session, PlayerSlot.Player1);
        var code = _earlier.Codes.Of(table.Seats[0]).ShouldNotBeNull();
        var journal = await _before.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain($"Session {table.Id} rebuilt from {journal.Count} recorded decisions");
        var rebuilt = _later.ById(table.Id).ShouldNotBeNull();
        rebuilt.Session.MatchId.ShouldBe(table.Session.MatchId);
        rebuilt.Seats[0].Token.ShouldBe(table.Seats[0].Token);
        rebuilt.Seats[1].Token.ShouldBe(table.Seats[1].Token);
        rebuilt.Pilot.Token.ShouldBe(table.Pilot.Token);
        _later.ByToken(table.Seats[0].Token).ShouldBeSameAs(rebuilt);
        _later.Codes.Of(rebuilt.Seats[0]).ShouldBe(code);
        rebuilt.CreatedAt.ShouldBe(table.CreatedAt);

        var waiting = await Waiting(rebuilt.Seats[0].Person!);
        waiting.Kind.ShouldBe(asked.Kind);
        waiting.Creature.ShouldBe(asked.Creature);
        (await Board(rebuilt.Session, PlayerSlot.Player1)).ShouldBe(board);
        (await _after.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken)).Count.ShouldBe(journal.Count, "a replay writes nothing it read");

        // And the game goes on from there: the person's next decision is taken by the rebuilt table and written down.
        rebuilt.Seats[0].Person!.Submit(PlayerDecision.DeclareIntent(waiting.Creature!.Value, SpellId.Parse("spell:strike:v1")), waiting.Asked).ShouldBeTrue();
        for (var attempt = 0; attempt < 300 && (await _after.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken)).Count == journal.Count; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        (await _after.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken)).Count.ShouldBe(journal.Count + 1);
    }

    [Fact]
    public async Task A_finished_table_a_closed_one_and_a_run_without_a_record_are_left_alone()
    {
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);
        var closed = await Opened(HostedTables.OnePerson);
        _earlier.Remove(closed.Id).ShouldBeTrue();
        await Marked(closed.Id, TableRecord.Closed);
        await new FileArtifactStore(_runs).Writer("20200101-000000-aaaaaaaa").WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);
        (await _before.Stored!.RecordAsync(finished.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldBeEmpty();
        _later.All().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_table_nobody_has_been_at_for_longer_than_the_host_would_keep_it_is_left_as_it_was()
    {
        var table = await Opened(HostedTables.OnePerson);
        table.Seats[0].Person!.Submit(PlayerDecision.Pass, (await Waiting(table.Seats[0].Person!)).Asked).ShouldBeTrue();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var later = new Clock { Now = DateTimeOffset.UtcNow + TableRegistry.IdleFor + TimeSpan.FromMinutes(1) };

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, later, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("left as it was");
        _later.All().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_record_whose_decisions_no_longer_fit_the_match_is_not_rebuilt()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntilIntent(table, table.Seats[0].Person!);
        var store = new FileArtifactStore(_runs);
        var journal = (await _before.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken)).ToList();
        journal[1] = journal[1] with { Slot = journal[1].Slot == "player1" ? "player2" : "player1" };
        await store.Writer(table.Id).StartJsonLinesAsync(DecisionJournal.File, TestContext.Current.CancellationToken);
        await store.Writer(table.Id).AppendJsonLinesAsync(DecisionJournal.File, journal, TestContext.Current.CancellationToken);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        _later.All().ShouldBeEmpty("a line that does not answer the question asked is a divergence, not a guess");
        said.ShouldBeEmpty("the composer says why on the console");
    }

    /// <summary>
    /// A record can fit a match move for move and still be another match -- the same legal moves under other
    /// content or another engine -- and the boards the earlier host checkpointed are what says so.
    /// </summary>
    [Fact]
    public async Task A_rebuilt_match_that_does_not_agree_with_the_checkpointed_boards_is_not_kept()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntilIntent(table, table.Seats[0].Person!);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var path = Path.Combine(_runs, table.Id, "traces", $"{table.Session.MatchId}.json");
        var trace = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!;
        var entries = trace["entries"]!.AsArray();
        entries[^1]!["player1"]!["allies"]![0]!["health"] = 1;
        await File.WriteAllTextAsync(path, trace.ToJsonString(), TestContext.Current.CancellationToken);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        _later.All().ShouldBeEmpty("the rebuilt boards are not the boards that were checkpointed");
        said.ShouldBeEmpty();
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await _before.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        _earlier.TryAdd(table).ShouldBeTrue();
        await table.RecordAsync(_earlier.Codes, TestContext.Current.CancellationToken);
        return table;
    }

    /// <summary>
    /// Passes the evolution, then goes Quick with every creature, keeping any tie order, until the seat is asked
    /// an intent. Through the table's API, as a page plays: that is where a decision is checkpointed into the
    /// trace, which is what a rebuild is held against.
    /// </summary>
    private static async Task PlayUntilIntent(PlayedTable table, HumanSeat person)
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            switch (person.Waiting)
            {
                case { Kind: PlayerOptionsKind.Intent }:
                    return;
                case { Kind: PlayerOptionsKind.Evolution } question:
                    await Post(table, question, """{"kind":"Evolution","pass":true}""");
                    break;
                case { Kind: PlayerOptionsKind.Speed, Creature: { } creature } question:
                    await Post(table, question, $$"""{"kind":"Speed","creature":{{creature.Value}},"speed":"Quick"}""");
                    break;
                case { Kind: PlayerOptionsKind.TieOrder } question:
                    var options = await table.Session.Queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(table.Session.MatchId, PlayerSlot.Player1), TestContext.Current.CancellationToken);
                    var order = options.Value.TieOrder!.Ties.SelectMany(group => group).Select(creature => creature.Value);
                    await Post(table, question, $$"""{"kind":"TieOrder","order":[{{string.Join(",", order)}}]}""");
                    break;
                default:
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                    break;
            }
        }

        throw new InvalidOperationException($"The seat never reached Intent; it is waiting for {person.Waiting?.Kind.ToString() ?? "nothing"}.");
    }

    private static async Task Post(PlayedTable table, HumanSeat.Question question, string body)
    {
        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", body.Insert(1, $"\"asked\":{question.Asked},"), table.Seats[0].Token);
        answer.Status.ShouldBe(204, System.Text.Encoding.UTF8.GetString(answer.Body));
    }

    private static async Task<HumanSeat.Question> Waiting(HumanSeat person)
    {
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return person.Waiting.ShouldNotBeNull();
    }

    private static async Task<string> Board(TableSession session, PlayerSlot slot)
    {
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, slot), TestContext.Current.CancellationToken);
        board.IsSuccess.ShouldBeTrue();
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }

    private static async Task Finished(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue();
    }

    private async Task Marked(string id, string status)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if ((await _before.Stored!.RecordAsync(id, TestContext.Current.CancellationToken))?.Status == status)
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"Session {id} was never marked {status}.");
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
