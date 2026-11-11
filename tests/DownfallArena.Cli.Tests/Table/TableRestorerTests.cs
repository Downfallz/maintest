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
/// A host that starts over a store picks up the tables the host before it left unfinished, from their journals
/// (ADR 0159, after ADR 0091): same board, same question, same links and codes. Nothing is replayed as it starts
/// (ADR 0161): the start-up pass puts the open records back into the index, and a table is picked up at the
/// first request that names it. Two hosts over one store stand in for the one before and the one after a
/// redeploy; the first is never disposed before the second has picked up, because a replica that went away did
/// not close anything.
/// </summary>
public sealed class TableRestorerTests : IDisposable
{
    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-restore-{Guid.NewGuid():N}");
    private readonly HostedTables _before;
    private readonly HostedTables _after;
    private readonly TableRegistry _earlier;
    private TableRegistry? _later;

    public TableRestorerTests()
    {
        _before = new HostedTables(_ => new FileArtifactStore(_runs));
        _after = new HostedTables(_ => new FileArtifactStore(_runs));
        _earlier = _before.Registry();
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _later?.Dispose();
        _earlier.Dispose();
        _after.Dispose();
        _before.Dispose();
        if (Directory.Exists(_runs))
        {
            Directory.Delete(_runs, recursive: true);
        }
    }

    [Fact]
    public async Task The_start_up_pass_picks_up_no_table()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);

        var later = await Restarted();

        later.HeldTables().ShouldBeEmpty("a table is picked up at the first request that names it, not as the host starts");
        later.Held(table.Id).ShouldBeNull();
    }

    [Fact]
    public async Task The_start_up_pass_says_how_many_open_tables_it_indexed_in_one_line()
    {
        await Opened(HostedTables.OnePerson);
        await Opened(HostedTables.OnePerson);
        _later = _after.Registry();

        var said = await TableRestorer.RestoreAsync(_after.Stored!, _later, Cancel);

        said.ShouldHaveSingleItem().ShouldStartWith("2 open tables indexed");
    }

    /// <summary>An index one write behind its records -- written before it existed, or a write that failed -- is put right as a host starts.</summary>
    [Fact]
    public async Task An_open_record_missing_from_the_index_is_listed_again_by_the_start_up_pass()
    {
        var table = await Opened(HostedTables.OnePerson);
        (await _after.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();

        await Restarted();

        (await _after.Index.OpenAsync(0, 10, Cancel)).ShouldHaveSingleItem().Table.ShouldBe(table.Id);
    }

    [Fact]
    public async Task A_seat_s_code_reaches_its_table_through_the_index_the_start_up_pass_put_right()
    {
        var table = await Opened(HostedTables.OnePerson);
        var code = table.Seats[0].Code.ShouldNotBeNull();
        var key = table.Seats[0].Key.ShouldNotBeNull();
        var later = await Restarted();

        var answer = await later.AnswerAsync($"{JoinCodes.Prefix}{code}", Cancel);

        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={key}");
    }

    /// <summary>A key minted before keys named their table (4.3a) names none: the start-up pass tells the host which table it reaches.</summary>
    [Fact]
    public async Task A_legacy_seat_token_reaches_its_table_after_the_start_up_pass()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        _earlier.Dispose();
        await Task.WhenAny(table.Session.Outcome);
        var legacy = Guid.NewGuid().ToString("N");
        var path = Path.Combine(_runs, table.Id, TableRecord.File);
        var record = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, Cancel))!;
        record["seats"]![0]!["token"] = legacy;
        await File.WriteAllTextAsync(path, record.ToJsonString(), Cancel);
        var later = await Restarted();

        var reached = await later.ByTokenAsync(legacy, Cancel);

        reached.ShouldNotBeNull().Id.ShouldBe(table.Id);
        reached.Seats[0].Token.ShouldBe(legacy);
    }

    [Fact]
    public async Task A_legacy_table_picked_up_preserves_every_existing_sequence_and_stays_legacy_when_recorded_again()
    {
        var table = await Opened(HostedTables.OnePerson);
        var person = table.Seats[0].Person!;
        await PlayUntil(table, person, PlayerOptionsKind.Intent);
        await table.Run!.CheckpointAsync(table.Session.MatchId, Cancel);
        var path = Path.Combine(table.Run.Location, "traces", $"{table.Session.MatchId}.json");
        var trace = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, Cancel))!;
        trace.AsObject().Remove("eventStreamVersion");
        var entries = trace["entries"]!.AsArray();
        var legacy = entries.Where(entry => entry!["event"]!["kind"]!.GetValue<string>() != "RoundEnergyGranted").Select(entry => entry!.DeepClone()).ToArray();
        entries.Clear();
        for (var i = 0; i < legacy.Length; i++)
        {
            legacy[i]["sequence"] = i;
            entries.Add(legacy[i]);
        }

        await File.WriteAllTextAsync(path, trace.ToJsonString(), Cancel);
        var recordPath = Path.Combine(table.Run.Location, TableRecord.File);
        var record = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(recordPath, Cancel))!;
        record.AsObject().Remove("eventStreamVersion");
        await File.WriteAllTextAsync(recordPath, record.ToJsonString(), Cancel);
        var later = await Restarted();

        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        rebuilt.EventStreamVersion.ShouldBe(1);
        var waiting = await Waiting(rebuilt.Seats[0].Person!);
        waiting.Kind.ShouldBe(person.Waiting!.Kind);
        waiting.Creature.ShouldBe(person.Waiting.Creature);
        var feed = rebuilt.Run!.Entries(rebuilt.Session.MatchId, 0);
        feed.Select(entry => entry.Sequence).ShouldBe(Enumerable.Range(0, legacy.Length));
        feed.ShouldNotContain(entry => entry.Event is DownfallArena.Domain.Matches.Events.RoundEnergyGranted);
        (await Board(rebuilt.Session, PlayerSlot.Player1)).ShouldBe(await Board(table.Session, PlayerSlot.Player1));
        var rewritten = TableRecord.Of(rebuilt, rebuilt.Request!);
        rewritten.EventStreamVersion.ShouldBe(1);
        await rebuilt.Run.RecordAsync(rewritten, Cancel);
        using var savedRecord = JsonDocument.Parse(await File.ReadAllTextAsync(recordPath, Cancel));
        savedRecord.RootElement.GetProperty("eventStreamVersion").GetInt32().ShouldBe(1);
        await rebuilt.Run.CheckpointAsync(rebuilt.Session.MatchId, Cancel);
        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(path, Cancel));
        saved.RootElement.GetProperty("eventStreamVersion").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task An_unknown_event_stream_version_is_preserved_and_refused_when_picked_up()
    {
        var table = await Opened(HostedTables.OnePerson);
        var path = Path.Combine(table.Run!.Location, TableRecord.File);
        var record = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, Cancel))!;
        record["eventStreamVersion"] = 999;
        var written = record.ToJsonString();
        await File.WriteAllTextAsync(path, written, Cancel);
        var later = await Restarted();

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull();
        (await File.ReadAllTextAsync(path, Cancel)).ShouldBe(written);
    }

    [Fact]
    public async Task A_table_a_person_was_playing_is_picked_up_at_the_same_question_with_the_same_links_and_codes()
    {
        var table = await Opened(HostedTables.OnePerson);
        var person = table.Seats[0].Person!;
        await PlayUntil(table, person, PlayerOptionsKind.Intent);
        var asked = person.Waiting.ShouldNotBeNull();
        var board = await Board(table.Session, PlayerSlot.Player1);
        var code = table.Seats[0].Code.ShouldNotBeNull();
        var journal = await JournalAsync(table.Id);
        var later = await Restarted();

        var rebuilt = (await later.ByTokenAsync(table.Seats[0].Token, Cancel)).ShouldNotBeNull();

        rebuilt.Id.ShouldBe(table.Id);
        rebuilt.ShouldNotBeSameAs(table);
        rebuilt.Session.MatchId.ShouldBe(table.Session.MatchId);
        rebuilt.Seats[0].Token.ShouldBe(table.Seats[0].Token);
        rebuilt.Seats[1].Token.ShouldBe(table.Seats[1].Token);
        rebuilt.Pilot.Token.ShouldBe(table.Pilot.Token);
        (await later.ByIdAsync(table.Id, Cancel)).ShouldBeSameAs(rebuilt);
        rebuilt.Seats[0].Code.ShouldBe(code);
        rebuilt.CreatedAt.ShouldBe(table.CreatedAt);

        var waiting = await Waiting(rebuilt.Seats[0].Person!);
        waiting.Kind.ShouldBe(asked.Kind);
        waiting.Creature.ShouldBe(asked.Creature);
        (await Board(rebuilt.Session, PlayerSlot.Player1)).ShouldBe(board);
        (await JournalAsync(table.Id)).Count.ShouldBe(journal.Count, "a replay writes nothing it read");

        // And the game goes on from there: the person's next decision is taken by the picked-up table and written down.
        await Post(rebuilt, waiting, $$"""{"kind":"Intent","creature":{{waiting.Creature!.Value.Value}},"spell":"spell:strike:v1"}""");

        (await JournalAsync(table.Id)).Count.ShouldBe(journal.Count + 1);
    }

    /// <summary>Requests that race for a table nobody holds share one pick-up: a table picked up twice would be two matches over one journal.</summary>
    [Fact]
    public async Task Requests_that_race_for_a_table_share_one_pick_up()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        var later = await Restarted();

        var picked = await Task.WhenAll(
            later.ByIdAsync(table.Id, Cancel),
            later.ByTokenAsync(table.Seats[0].Token, Cancel),
            later.ByTokenAsync(table.Pilot.Token, Cancel));

        picked[0].ShouldNotBeNull();
        picked.ShouldAllBe(one => one == picked[0]);
        later.HeldTables().ShouldHaveSingleItem();
    }

    /// <summary>
    /// A table opened to choose spells at the slot (ADR 0110) comes back choosing them there: the record keeps
    /// the declaration it played, so the replay feeds its actions to a match that asks for them, whatever the
    /// host's own rule set says.
    /// </summary>
    [Fact]
    public async Task A_table_that_chooses_spells_at_the_slot_is_picked_up_in_that_declaration_at_the_same_question()
    {
        var table = await Opened(HostedTables.OnePerson with { Declaration = CombatDeclaration.AtActivation });
        var person = table.Seats[0].Person!;
        await PlayUntil(table, person, PlayerOptionsKind.Action);
        var asked = person.Waiting.ShouldNotBeNull();
        var options = await table.Session.SnapshotAsync(PlayerSlot.Player1);
        var offered = options.Value.Options.Action.ShouldNotBeNull().Spells[0];
        await Post(table, asked, $$"""{"kind":"Action","creature":{{asked.Creature!.Value.Value}},"spell":"{{offered.Spell}}","targets":[{{offered.LegalTargets.Candidates[0].Value}}]}""");
        var next = await Waiting(person);
        var board = await Board(table.Session, PlayerSlot.Player1);
        var journal = await JournalAsync(table.Id);
        var later = await Restarted();

        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        (await _before.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Declaration.ShouldBe("at-activation");
        journal.ShouldContain(entry => (string?)entry["kind"] == "Action");
        journal.ShouldNotContain(entry => (string?)entry["kind"] == "Intent" || (string?)entry["kind"] == "Target");
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
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        await Finished(finished);
        var closed = await Opened(HostedTables.OnePerson);
        (await _earlier.RemoveAsync(closed.Id, Cancel)).ShouldBeTrue();
        await closed.Closing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        (await _before.Stored!.RecordAsync(closed.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
        const string bare = "20200101-000000-aaaaaaaa";
        await new FileArtifactStore(_runs).Writer(bare).WriteJsonAsync("manifest.json", new { Matches = 0 }, Cancel);
        (await _before.Stored!.RecordAsync(finished.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
        var later = await Restarted();

        string[] ids = [finished.Id, closed.Id, bare];
        var picked = await Task.WhenAll(ids.Select(id => later.ByIdAsync(id, Cancel)));

        picked.ShouldAllBe(table => table == null);
        later.HeldTables().ShouldBeEmpty();
        (await _after.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
    }

    /// <summary>
    /// Two people who both reached their seats began the match, and a host that picks the table up does not wait
    /// for them again: the record says it began, though neither has decided anything yet (ADR 0092, ADR 0159).
    /// </summary>
    [Fact]
    public async Task A_table_whose_people_had_all_reached_their_seats_is_picked_up_begun_before_any_decision()
    {
        var table = await Opened(HostedTables.TwoPeople);
        table.Session.HasBegun.ShouldBeTrue();
        (await JournalAsync(table.Id)).ShouldBeEmpty();
        var later = await Restarted();

        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        rebuilt.Session.HasBegun.ShouldBeTrue();
        (await Waiting(rebuilt.Seats[0].Person!)).Kind.ShouldBe(PlayerOptionsKind.Evolution);
    }

    /// <summary>
    /// A person who reached their seat before the host stopped is not waited for again: the match begins the
    /// moment the other person reaches theirs, at whichever host they reach it.
    /// </summary>
    [Fact]
    public async Task A_person_who_had_reached_their_seat_is_not_waited_for_again()
    {
        var table = await Recorded(HostedTables.TwoPeople);
        (await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Seats[0].Token)).Status.ShouldBe(200);
        var later = await Restarted();
        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        var waited = rebuilt.Session.HasBegun;

        (await rebuilt.Api.HandleAsync("GET", "/api/seat/player2", string.Empty, rebuilt.Seats[1].Token)).Status.ShouldBe(200);

        waited.ShouldBeFalse();
        rebuilt.Seats[0].Person!.HasArrived.ShouldBeTrue();
        rebuilt.Session.HasBegun.ShouldBeTrue();
    }

    /// <summary>The host's own table is recorded as open the moment it is; picked up beside itself it would be the same match twice.</summary>
    [Fact]
    public async Task A_table_this_host_already_holds_is_not_picked_up_beside_itself()
    {
        var table = await Opened(HostedTables.OnePerson);
        await TableRestorer.RestoreAsync(_before.Stored!, _earlier, Cancel);

        var found = await _earlier.ByIdAsync(table.Id, Cancel);

        found.ShouldBeSameAs(table);
        _earlier.HeldTables().ShouldHaveSingleItem().ShouldBeSameAs(table);
    }

    /// <summary>A table nobody ever played a decision at, opened longer ago than the sweep keeps one, is not picked up (ADR 0115).</summary>
    [Fact]
    public async Task A_table_that_never_began_is_not_picked_up_after_a_night()
    {
        // Opened and recorded, and nobody ever reached a seat: the match never began and no decision was taken.
        var table = await Recorded(HostedTables.OnePerson);
        var later = await Restarted(new Clock { Now = table.CreatedAt + TableRegistry.NeverBegunFor + TimeSpan.FromMinutes(1) });

        var picked = await later.ByTokenAsync(table.Seats[0].Token, Cancel);

        picked.ShouldBeNull();
        later.HeldTables().ShouldBeEmpty();
        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open, "left as it was, for the sweep to close");
    }

    /// <summary>The sweep closes a table that never began through its record, whoever holds it, or nobody: the next host does not find it open.</summary>
    [Fact]
    public async Task A_table_that_never_began_is_closed_through_its_record_by_the_sweep_after_a_night()
    {
        var table = await Recorded(HostedTables.OnePerson);
        var clock = new Clock { Now = table.CreatedAt + TableRegistry.NeverBegunFor + TimeSpan.FromMinutes(1) };
        var later = await Restarted(clock);

        var swept = await later.SweepAsync(clock.Now, Cancel);

        swept.ShouldBe([table.Id]);
        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
        (await _after.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
    }

    /// <summary>
    /// A record whose beginning was never written may stand over a journal of decisions: the sweep reads the journal
    /// as a pick-up does, and leaves a game in progress open.
    /// </summary>
    [Fact]
    public async Task The_sweep_leaves_open_a_table_whose_journal_holds_decisions_its_record_never_said_began()
    {
        var table = await Opened(HostedTables.OnePerson);
        await Post(table, await Waiting(table.Seats[0].Person!), """{"kind":"Evolution","pass":true}""");
        await table.QuiesceAsync(Cancel);
        _earlier.Dispose();
        await NeverBegunAsync(table.Id);
        var clock = new Clock { Now = table.CreatedAt + TableRegistry.NeverBegunFor + TimeSpan.FromMinutes(1) };
        var later = await Restarted(clock);

        var swept = await later.SweepAsync(clock.Now, Cancel);

        swept.ShouldBeEmpty();
        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open);
        (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task An_open_table_is_picked_up_even_after_a_long_break()
    {
        var table = await Opened(HostedTables.OnePerson);
        await Post(table, await Waiting(table.Seats[0].Person!), """{"kind":"Evolution","pass":true}""");
        var later = await Restarted(new Clock { Now = DateTimeOffset.UtcNow + TimeSpan.FromDays(30) });

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_record_whose_decisions_no_longer_fit_the_match_is_not_picked_up()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        var journal = await JournalAsync(table.Id);
        journal[1]["slot"] = (string?)journal[1]["slot"] == "player1" ? "player2" : "player1";
        await RewriteAsync(table.Id, journal);
        var later = await Restarted();

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull("a line that does not answer the question asked is a divergence, not a guess");
        later.HeldTables().ShouldBeEmpty();
    }

    /// <summary>
    /// A journal with a line no host can read is that table's problem and no other's: it is left as it was, and
    /// the other tables are picked up.
    /// </summary>
    [Fact]
    public async Task A_journal_with_a_line_it_cannot_read_refuses_only_its_own_table()
    {
        var broken = await Opened(HostedTables.OnePerson);
        await PlayUntil(broken, broken.Seats[0].Person!, PlayerOptionsKind.Intent);
        var sound = await Opened(HostedTables.OnePerson);
        await PlayUntil(sound, sound.Seats[0].Person!, PlayerOptionsKind.Intent);
        var journal = await JournalAsync(broken.Id);
        journal.First(line => (string?)line["kind"] == "Speed").Remove("speed");
        await RewriteAsync(broken.Id, journal);
        var later = await Restarted();

        var refused = await later.ByIdAsync(broken.Id, Cancel);
        var picked = await later.ByIdAsync(sound.Id, Cancel);

        refused.ShouldBeNull();
        picked.ShouldNotBeNull();
    }

    /// <summary>
    /// A record can fit a match move for move and still be another match -- the same legal moves under other
    /// content or another engine -- and the boards the earlier host checkpointed are what says so.
    /// </summary>
    [Fact]
    public async Task A_picked_up_match_that_does_not_agree_with_the_checkpointed_boards_is_not_kept()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Task.Delay(200, Cancel);
        var path = Path.Combine(_runs, table.Id, "traces", $"{table.Session.MatchId}.json");
        var trace = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, Cancel))!;
        var entries = trace["entries"]!.AsArray();
        entries[^1]!["player1"]!["allies"]![0]!["health"] = 1;
        await File.WriteAllTextAsync(path, trace.ToJsonString(), Cancel);
        var later = await Restarted();

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull("the rebuilt boards are not the boards that were checkpointed");
        later.HeldTables().ShouldBeEmpty();
    }

    /// <summary>
    /// A host stopping lets its tables go without closing them: the next host picking them up is the point.
    /// Only the operator closing a table, or the sweep, marks a record closed.
    /// </summary>
    [Fact]
    public async Task A_host_that_stops_leaves_its_open_tables_open_for_the_next_one()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);

        _earlier.Dispose();
        await Task.WhenAny(table.Session.Outcome);

        (await _before.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open);
        var later = await Restarted();
        (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
    }

    /// <summary>A picked-up table ends like any other: closed by the operator, its record says so, and no third host picks it up.</summary>
    [Fact]
    public async Task A_picked_up_table_closed_by_the_operator_marks_its_record_closed()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        var later = await Restarted();
        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        (await later.RemoveAsync(rebuilt.Id, Cancel)).ShouldBeTrue();
        await rebuilt.Closing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Closed);
    }

    /// <summary>A replay refused leaves the run exactly as the earlier host wrote it: its manifest, its dataset, its trace.</summary>
    [Fact]
    public async Task A_replay_that_is_refused_leaves_the_run_as_the_earlier_host_wrote_it()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Diverged(table);
        var before = await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl");
        var later = await Restarted();

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull();
        (await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl")).ShouldBe(before);
        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Open, "left as it was, not closed");
    }

    /// <summary>
    /// A record refused under an engine, content and rules is marked with them when the pick-up is refused, and
    /// kept open for the operator.
    /// </summary>
    [Fact]
    public async Task A_refused_pick_up_is_marked_in_the_record_and_the_table_kept_open()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Diverged(table);
        using var refusing = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("abc123"));
        using var registry = await refusing.RestartedAsync();

        var picked = await registry.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull();
        var record = (await refusing.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        record.RefusedUnder.ShouldBe(Basis("abc123"));
        record.Status.ShouldBe(TableRecord.Open, "kept for the operator, not closed");
    }

    /// <summary>
    /// A record refused once under an engine, content and rules is refused again under the same ones, and only after
    /// the time a replay takes: a host on the same basis does not try it again, even with a journal it could now replay.
    /// </summary>
    [Fact]
    public async Task A_host_on_the_same_basis_does_not_try_a_refused_record_again()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Task.Delay(200, Cancel);
        var journal = await JournalAsync(table.Id);
        await Diverged(table);
        using var refusing = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("abc123"));
        using (var refused = await refusing.RestartedAsync())
        {
            (await refused.ByIdAsync(table.Id, Cancel)).ShouldBeNull();
        }

        await RewriteAsync(table.Id, journal);
        using var again = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("abc123"));
        using var registry = await again.RestartedAsync();

        var picked = await registry.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull("the journal would replay now, and it is not tried");
        (await again.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().RefusedUnder.ShouldBe(Basis("abc123"));
    }

    /// <summary>A record a host on this basis refused is the operator's, not the lobby's: the start-up pass leaves it out of the index (ADR 0115).</summary>
    [Fact]
    public async Task A_record_refused_under_this_host_s_basis_is_not_listed_by_it_after_the_start_up_pass()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Refused(table, Basis("abc123"));
        using var again = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("abc123"));

        using var registry = await again.RestartedAsync();

        (await registry.OpenAsync(Cancel)).ShouldBeEmpty();
    }

    /// <summary>Another engine, other content or other rules may replay what an earlier one could not, so the lobby lists it again.</summary>
    [Fact]
    public async Task A_record_refused_under_another_basis_is_listed_again_by_the_start_up_pass()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Refused(table, Basis("abc123"));
        using var refusing = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("abc123"));
        using var first = await refusing.RestartedAsync();
        (await first.OpenAsync(Cancel)).ShouldBeEmpty();

        // The same index the refusing host kept, as a store's index outlives its host.
        using var newer = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("def456"), index: refusing.Index);
        using var registry = await newer.RestartedAsync();

        (await registry.OpenAsync(Cancel)).ShouldHaveSingleItem().Id.ShouldBe(table.Id);
    }

    /// <summary>Another engine, other content or other rules may replay what an earlier one could not, so it tries.</summary>
    [Fact]
    public async Task A_host_on_another_basis_tries_a_refused_record_again()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        await Refused(table, Basis("abc123"));
        using var newer = new HostedTables(_ => new FileArtifactStore(_runs), basis: Basis("def456"));
        using var registry = await newer.RestartedAsync();

        var picked = await registry.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull("replayed, and refused again");
        (await newer.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().RefusedUnder.ShouldBe(Basis("def456"));
    }

    /// <summary>
    /// The journal is the table (ADR 0159). A checkpoint that runs past it -- a decision applied and seen by a
    /// checkpoint, and never written down -- is of a match the journal does not hold: the table comes back
    /// where its journal ends, held against the checkpoint as far as the two go together.
    /// </summary>
    [Fact]
    public async Task A_journal_that_stops_short_of_the_checkpoint_is_the_table_as_far_as_it_goes()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        var journal = await JournalAsync(table.Id);
        var kept = journal.Take(journal.FindLastIndex(entry => (string?)entry["slot"] == "player1" && (string?)entry["kind"] == "Speed")).ToList();
        await RewriteAsync(table.Id, kept);
        var later = await Restarted();

        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        (await Waiting(rebuilt.Seats[0].Person!)).Kind.ShouldBe(PlayerOptionsKind.Speed);
    }

    private static ReplayBasis Basis(string engine) => new(engine, "content", Application.Learning.RuleSetStamp.Of(HostedTables.Rules));

    /// <summary>The record's second decision handed to the other seat: a line that no longer answers the question asked.</summary>
    private async Task Diverged(PlayedTable table)
    {
        await Task.Delay(200, Cancel);
        var journal = await JournalAsync(table.Id);
        journal[1]["slot"] = (string?)journal[1]["slot"] == "player1" ? "player2" : "player1";
        await RewriteAsync(table.Id, journal);
    }

    /// <summary>The table's journal diverged, and its pick-up refused by a host on this basis, which marks its record.</summary>
    private async Task Refused(PlayedTable table, ReplayBasis basis)
    {
        await Diverged(table);
        using var refusing = new HostedTables(_ => new FileArtifactStore(_runs), basis: basis);
        using var registry = await refusing.RestartedAsync();
        (await registry.ByIdAsync(table.Id, Cancel)).ShouldBeNull();
        (await refusing.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().RefusedUnder.ShouldBe(basis);
    }

    /// <summary>
    /// A table already under way is counted by the index before anybody picks it up, so picking it up takes no
    /// place of its own: a host whose places are all taken still picks it up when a request names it.
    /// </summary>
    [Fact]
    public async Task A_table_under_way_is_picked_up_on_a_host_with_no_place_left()
    {
        var table = await Opened(HostedTables.OnePerson);
        await PlayUntil(table, table.Seats[0].Person!, PlayerOptionsKind.Intent);
        var later = await Restarted(mostUnderWay: 1);
        (await later.ReserveAsync(Cancel)).ShouldBeNull("the table the index lists fills the one place");

        var picked = await later.ByIdAsync(table.Id, Cancel);

        picked.ShouldNotBeNull();
    }

    /// <summary>
    /// A host that died after writing the decision that ended the match, and before marking the table finished,
    /// left a record that ends the match when replayed. That is a finished table recovered, not a replay that
    /// failed: its links answer, and its record is marked finished so no host picks it up again.
    /// </summary>
    [Fact]
    public async Task A_record_whose_last_decision_ends_the_match_is_picked_up_as_a_finished_table()
    {
        var table = await Opened(HostedTables.Bots);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        await Finished(table);
        var record = (await _before.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        await new FileArtifactStore(_runs).Writer(table.Id).WriteJsonAsync(TableRecord.File, record with { Status = TableRecord.Open }, Cancel);
        var dataset = await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl");
        var later = await Restarted();

        var rebuilt = (await later.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        await rebuilt.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        await Finished(rebuilt);
        for (var attempt = 0; attempt < 300 && (await _after.Stored!.RecordAsync(table.Id, Cancel))?.Status != TableRecord.Finished; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        (await _after.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
        var again = await Files(table.Id, "manifest.json", "steps.jsonl", "episodes.jsonl");
        Lines(again[1]).ShouldBe(Lines(dataset[1]), "the steps the earlier host wrote are not written a second time");
        Lines(again[2]).ShouldBe(2, "an episode a seat");
    }

    /// <summary>
    /// The host after the earlier one, over the same store, after its start-up pass: <see cref="HostedTables.RestartedAsync" />,
    /// or, with a clock, the same over a loader that reads the time off it.
    /// </summary>
    private async Task<TableRegistry> Restarted(TimeProvider? clock = null, int mostUnderWay = TableRegistry.MostUnderWay)
    {
        if (clock is null)
        {
            return _later = await _after.RestartedAsync(mostUnderWay);
        }

        _later = new TableRegistry(_after.Index, mostUnderWay) { Loader = new TableLoader(_after.Stored!, _after.Composer, clock) };
        await TableRestorer.RestoreAsync(_after.Stored!, _later, Cancel);
        return _later;
    }

    private static int Lines(string? text) => text.ShouldNotBeNull().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The lines of a table's journal as they are written, to read or to rewrite.</summary>
    private async Task<List<System.Text.Json.Nodes.JsonObject>> JournalAsync(string id)
    {
        var path = Path.Combine(_runs, id, PlaytestRun.JournalFile);
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, Cancel) : string.Empty;
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => System.Text.Json.Nodes.JsonNode.Parse(line)!.AsObject())];
    }

    /// <summary>A table's record rewritten to say its match never began, as one whose beginning failed to be written says.</summary>
    private async Task NeverBegunAsync(string id)
    {
        var path = Path.Combine(_runs, id, TableRecord.File);
        var record = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, Cancel))!.AsObject();
        record["begun"] = false;
        await File.WriteAllTextAsync(path, record.ToJsonString(), Cancel);
    }

    /// <summary>A table's journal replaced by these lines: what an engine or content that changed under it would read.</summary>
    private async Task RewriteAsync(string id, IEnumerable<System.Text.Json.Nodes.JsonObject> lines) =>
        await File.WriteAllLinesAsync(Path.Combine(_runs, id, PlaytestRun.JournalFile), lines.Select(line => line.ToJsonString()), Cancel);

    private async Task<IReadOnlyList<string?>> Files(string id, params string[] names)
    {
        var reader = new FileArtifactStore(_runs).Reader(id);
        var texts = new List<string?>();
        foreach (var name in names)
        {
            texts.Add(await reader.ReadTextAsync(name, Cancel));
        }

        return texts;
    }

    /// <summary>A table opened at the earlier host and recorded, nobody at it yet.</summary>
    private async Task<PlayedTable> Recorded(TableRequest request)
    {
        var table = await _before.Composer.ComposeAsync(request, Cancel);
        using var place = (await _earlier.ReserveAsync(Cancel)).ShouldNotBeNull();
        await _earlier.AddAsync(table, place, Cancel);
        await table.RecordAsync(Cancel);
        return table;
    }

    private async Task<PlayedTable> Opened(TableRequest request)
    {
        var table = await Recorded(request);

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
    private async Task PlayUntil(PlayedTable table, ISeatPerson person, PlayerOptionsKind kind)
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
                    var options = await table.Session.SnapshotAsync(PlayerSlot.Player1);
                    var order = options.Value.Options.TieOrder!.Ties.SelectMany(group => group).Select(creature => creature.Value);
                    await Post(table, question, $$"""{"kind":"TieOrder","order":[{{string.Join(",", order)}}]}""");
                    break;
                default:
                    await Task.Delay(10, Cancel);
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
                var journal = await JournalAsync(table.Id);
                if (journal.Count(entry => (string?)entry["slot"] == "player2" && (string?)entry["kind"] == "Intent") == HostedTables.Rules.TeamSize)
                {
                    return;
                }
            }
            catch (JsonException)
            {
                // A line still on its way into the file.
            }

            await Task.Delay(10, Cancel);
        }

        throw new InvalidOperationException("The bot in the other seat never wrote its intents down.");
    }

    private static async Task Post(PlayedTable table, Asking question, string body)
    {
        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", body.Insert(1, $"\"asked\":{question.Asked},"), table.Seats[0].Token);
        answer.Status.ShouldBe(204, System.Text.Encoding.UTF8.GetString(answer.Body));
    }

    private static async Task<Asking> Waiting(ISeatPerson person)
    {
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        return person.Waiting.ShouldNotBeNull();
    }

    private static async Task<string> Board(ITableSession session, PlayerSlot slot)
    {
        var board = await session.BoardAsync(slot);
        board.IsSuccess.ShouldBeTrue();
        return JsonSerializer.Serialize(board.Value, ArtifactJson.LineOptions);
    }

    private static async Task Finished(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        table.IsFinished.ShouldBeTrue();
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
