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
        await PlayUntil(table, person, PlayerOptionsKind.Intent);
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

    /// <summary>
    /// A table opened to choose spells at the slot (ADR 0110) comes back choosing them there: the record keeps
    /// the declaration it played, so the replay feeds its actions to a match that asks for them, whatever the
    /// host's own rule set says.
    /// </summary>
    [Fact]
    public async Task A_table_that_chooses_spells_at_the_slot_is_rebuilt_in_that_declaration_at_the_same_question()
    {
        var table = await Opened(HostedTables.OnePerson with { Declaration = CombatDeclaration.AtActivation });
        var person = table.Seats[0].Person!;
        await PlayUntil(table, person, PlayerOptionsKind.Action);
        var asked = person.Waiting.ShouldNotBeNull();
        var options = await table.Session.Queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(table.Session.MatchId, PlayerSlot.Player1), TestContext.Current.CancellationToken);
        var offered = options.Value.Action.ShouldNotBeNull().Spells[0];
        await Post(table, asked, $$"""{"kind":"Action","creature":{{asked.Creature!.Value.Value}},"spell":"{{offered.Spell}}","targets":[{{offered.LegalTargets.Candidates[0].Value}}]}""");
        var next = await Waiting(person);
        var board = await Board(table.Session, PlayerSlot.Player1);
        var journal = await _before.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken);

        await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        (await _before.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Declaration.ShouldBe("at-activation");
        journal.ShouldContain(entry => entry.Kind == "Action");
        journal.ShouldNotContain(entry => entry.Kind == "Intent" || entry.Kind == "Target");
        var rebuilt = _later.ById(table.Id).ShouldNotBeNull();
        rebuilt.Session.Rules.CombatDeclaration.ShouldBe(CombatDeclaration.AtActivation);
        var waiting = await Waiting(rebuilt.Seats[0].Person!);
        waiting.Kind.ShouldBe(next.Kind);
        waiting.Creature.ShouldBe(next.Creature);
        (await Board(rebuilt.Session, PlayerSlot.Player1)).ShouldBe(board);
    }

    [Fact]
    public async Task A_finished_table_a_closed_one_and_a_run_without_a_record_are_left_alone()
    {
        var finished = await Opened(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(finished);
        var closed = await Opened(HostedTables.OnePerson);
        _earlier.Remove(closed.Id).ShouldBeTrue();
        await closed.Closing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        (await _before.Stored!.RecordAsync(closed.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
        await new FileArtifactStore(_runs).Writer("20200101-000000-aaaaaaaa").WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);
        (await _before.Stored!.RecordAsync(finished.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldBeEmpty();
        _later.All().ShouldBeEmpty();
    }

    /// <summary>The host's own table is recorded as open the moment it is; rebuilt beside itself it would be the same match twice.</summary>
    [Fact]
    public async Task A_table_this_host_already_holds_is_not_rebuilt_beside_itself()
    {
        var table = await Opened(HostedTables.OnePerson);

        var said = await TableRestorer.RestoreAsync(_before.Stored!, _before.Composer, _earlier, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldBeEmpty();
        _earlier.All().ShouldHaveSingleItem().ShouldBeSameAs(table);
    }

    /// <summary>A table nobody ever played a decision at, opened longer ago than the sweep keeps one, is not rebuilt (ADR 0115).</summary>
    [Fact]
    public async Task A_table_that_never_began_is_left_as_it_was_after_a_night()
    {
        // Opened and recorded, and nobody ever reached a seat: the match never began and no decision was taken.
        var table = await _before.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _earlier.TryAdd(table).ShouldBeTrue();
        await table.RecordAsync(_earlier.Codes, TestContext.Current.CancellationToken);
        var later = new Clock { Now = table.CreatedAt + TableRegistry.NeverBegunFor + TimeSpan.FromMinutes(1) };

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, later, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("never began");
        _later.All().ShouldBeEmpty();
        (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open, "left as it was, not marked");
    }

    [Fact]
    public async Task An_open_table_is_rebuilt_even_after_a_long_break()
    {
        var table = await Opened(HostedTables.OnePerson);
        table.Seats[0].Person!.Submit(PlayerDecision.Pass, (await Waiting(table.Seats[0].Person!)).Asked).ShouldBeTrue();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var later = new Clock { Now = DateTimeOffset.UtcNow + TimeSpan.FromDays(30) };

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, later, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("rebuilt");
        _later.ById(table.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_record_whose_decisions_no_longer_fit_the_match_is_not_rebuilt()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
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
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
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

    /// <summary>
    /// A host stopping lets its tables go without closing them: the next host rebuilding them is the point.
    /// Only the operator closing a table, or the sweep, marks a record closed.
    /// </summary>
    [Fact]
    public async Task A_host_that_stops_leaves_its_open_tables_open_for_the_next_one()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);

        _earlier.Dispose();
        await Task.WhenAny(table.Session.Outcome);

        (await _before.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open);
        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);
        said.ShouldHaveSingleItem().ShouldContain("rebuilt");
        _later.ById(table.Id).ShouldNotBeNull();
    }

    /// <summary>A rebuilt table ends like any other: closed by the operator, its record says so, and no third host rebuilds it.</summary>
    [Fact]
    public async Task A_rebuilt_table_closed_by_the_operator_marks_its_record_closed()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);
        var rebuilt = _later.ById(table.Id).ShouldNotBeNull();

        _later.Remove(rebuilt.Id).ShouldBeTrue();
        await rebuilt.Closing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
    }

    /// <summary>A replay refused leaves the run exactly as the earlier host wrote it: its manifest, its dataset, its trace.</summary>
    [Fact]
    public async Task A_replay_that_is_refused_leaves_the_run_as_the_earlier_host_wrote_it()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var store = new FileArtifactStore(_runs);
        var journal = (await _before.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken)).ToList();
        journal[1] = journal[1] with { Slot = journal[1].Slot == "player1" ? "player2" : "player1" };
        await store.Writer(table.Id).StartJsonLinesAsync(DecisionJournal.File, TestContext.Current.CancellationToken);
        await store.Writer(table.Id).AppendJsonLinesAsync(DecisionJournal.File, journal, TestContext.Current.CancellationToken);
        var before = await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl");

        await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        _later.All().ShouldBeEmpty();
        (await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl")).ShouldBe(before);
        (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open, "left as it was, not closed");
    }

    /// <summary>The host's own table holds its place before the rebuild, so a store full of open tables cannot take it.</summary>
    [Fact]
    public async Task A_place_held_before_the_rebuild_is_not_taken_by_it()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        using var one = new TableRegistry(mostUnderWay: 1);
        using var held = one.Reserve().ShouldNotBeNull();

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, one, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("as many tables under way as it takes");
        one.All().ShouldBeEmpty();
        (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open, "left for a host with room");
    }

    /// <summary>
    /// A host that died after writing the decision that ended the match, and before marking the table finished,
    /// left a record that ends the match when replayed. That is a finished table recovered, not a replay that
    /// failed: its links answer, and its record is marked finished so no host rebuilds it again.
    /// </summary>
    [Fact]
    public async Task A_record_whose_last_decision_ends_the_match_is_rebuilt_as_a_finished_table()
    {
        var table = await Opened(HostedTables.Bots);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(table);
        var record = (await _before.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await new FileArtifactStore(_runs).Writer(table.Id).WriteJsonAsync(TableRecord.File, record with { Status = TableRecord.Open }, TestContext.Current.CancellationToken);

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _after.Composer, _later, TimeProvider.System, TestContext.Current.CancellationToken);

        said.ShouldHaveSingleItem().ShouldContain("rebuilt");
        var rebuilt = _later.ById(table.Id).ShouldNotBeNull();
        await rebuilt.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(rebuilt);
        for (var attempt = 0; attempt < 300 && (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken))?.Status != TableRecord.Finished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        (await _after.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
    }

    private async Task<IReadOnlyList<string?>> Files(string id, params string[] names)
    {
        var reader = new FileArtifactStore(_runs).Reader(id);
        var texts = new List<string?>();
        foreach (var name in names)
        {
            texts.Add(await reader.ReadTextAsync(name, TestContext.Current.CancellationToken));
        }

        return texts;
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await _before.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        _earlier.TryAdd(table).ShouldBeTrue();
        await table.RecordAsync(_earlier.Codes, TestContext.Current.CancellationToken);

        // Every person reaches their seat, which is what begins the match (ADR 0092).
        foreach (var seat in table.Seats.Where(seat => seat.Person is not null))
        {
            (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        }

        return table;
    }

    /// <summary>
    /// Passes the evolution, then goes Quick with every creature, keeping any tie order, until the seat is asked
    /// the kind named: an intent, or an action at the slot under a rule set that declares none (ADR 0110).
    /// Through the table's API, as a page plays: that is where a decision is checkpointed into the trace, which
    /// is what a rebuild is held against.
    /// </summary>
    private async Task PlayUntil(PlayedTable table, HumanSeat person, PlayerOptionsKind kind)
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            switch (person.Waiting)
            {
                case { Kind: PlayerOptionsKind.Intent } when kind == PlayerOptionsKind.Intent:
                    await OtherSeatDeclared(table);
                    return;
                case { Kind: var asked } when asked == kind:
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

        throw new InvalidOperationException($"The seat never reached {kind}; it is waiting for {person.Waiting?.Kind.ToString() ?? "nothing"}.");
    }

    /// <summary>
    /// The bot in the other seat is asked its intents at the same time as the person (ADR 0111), and writes each
    /// one down as it declares it. A test that reads the record waits for them, or it reads the record half way.
    /// </summary>
    private async Task OtherSeatDeclared(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            try
            {
                var journal = await _before.Stored!.JournalAsync(table.Id, TestContext.Current.CancellationToken);
                if (journal.Count(entry => entry.Slot == "player2" && entry.Kind == "Intent") == HostedTables.Rules.TeamSize)
                {
                    return;
                }
            }
            catch (JsonException)
            {
                // A line still on its way into the file.
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException("The bot in the other seat never wrote its intents down.");
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

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
