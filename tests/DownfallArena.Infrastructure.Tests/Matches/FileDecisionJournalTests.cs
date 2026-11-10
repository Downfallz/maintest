using System.Text.Json;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// The journal on disk is the <c>decisions.jsonl</c> the table has written since ADR 0091: same place, same
/// lines, so a table recorded before ADR 0159 is read by it and what it writes is read by what came before.
/// </summary>
public sealed class FileDecisionJournalTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-journal-{Guid.NewGuid():N}");
    private readonly FileDecisionJournal _journal;

    public FileDecisionJournalTests()
    {
        _journal = new FileDecisionJournal(_root, new At(Now));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_decision_is_one_line_of_the_run_s_decisions_file_numbered_and_stamped()
    {
        await _journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0], DecisionJournalTests.EveryKind[7]], TestContext.Current.CancellationToken);

        var lines = await File.ReadAllLinesAsync(Path.Combine(_root, "run-1", "decisions.jsonl"), TestContext.Current.CancellationToken);
        lines.Length.ShouldBe(2);
        using var bought = JsonDocument.Parse(lines[0]);
        bought.RootElement.GetProperty("seq").GetInt64().ShouldBe(1);
        bought.RootElement.GetProperty("at").GetDateTimeOffset().ShouldBe(Now);
        bought.RootElement.GetProperty("slot").GetString().ShouldBe("player1");
        bought.RootElement.GetProperty("kind").GetString().ShouldBe("Evolution");
        bought.RootElement.GetProperty("creature").GetInt32().ShouldBe(1);
        bought.RootElement.GetProperty("tier").GetString().ShouldBe("tier:guard:v1");
        bought.RootElement.GetProperty("pass").GetBoolean().ShouldBeFalse();
        using var acted = JsonDocument.Parse(lines[1]);
        acted.RootElement.GetProperty("seq").GetInt64().ShouldBe(2);
        acted.RootElement.GetProperty("slot").GetString().ShouldBe("player2");
        acted.RootElement.GetProperty("kind").GetString().ShouldBe("Action");
        acted.RootElement.GetProperty("spell").GetString().ShouldBe("spell:guard:v1");
        acted.RootElement.GetProperty("targets").EnumerateArray().Select(target => target.GetInt32()).ShouldBe([3]);
    }

    [Fact]
    public async Task A_line_that_is_no_decision_is_counted_and_carries_none()
    {
        Directory.CreateDirectory(Path.Combine(_root, "run-1"));
        var path = Path.Combine(_root, "run-1", "decisions.jsonl");
        await File.WriteAllTextAsync(path, """{"seq":1,"at":"2026-11-10T14:00:00+00:00","slot":"player1","kind":"Swap","to":"greedy","atRound":3,"pass":false}""" + "\n", TestContext.Current.CancellationToken);

        var reading = await _journal.ReadAsync("run-1", TestContext.Current.CancellationToken);
        await _journal.AppendAsync("run-1", reading.Position, [DecisionJournalTests.EveryKind[1]], TestContext.Current.CancellationToken);

        reading.Decisions.ShouldBeEmpty();
        reading.Position.Lines.ShouldBe(1);
        var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);
        using var passed = JsonDocument.Parse(lines[1]);
        passed.RootElement.GetProperty("seq").GetInt64().ShouldBe(2);
        (await _journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Decisions.ShouldBe([DecisionJournalTests.EveryKind[1]]);
    }

    [Fact]
    public async Task A_line_another_process_wrote_refuses_the_append_that_did_not_read_it()
    {
        var read = (await _journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var path = Path.Combine(_root, "run-1", "decisions.jsonl");
        await File.AppendAllTextAsync(path, """{"seq":2,"at":"2026-11-10T14:00:00+00:00","slot":"player2","kind":"Evolution","pass":true}""" + "\n", TestContext.Current.CancellationToken);

        var late = await _journal.AppendAsync("run-1", read, [DecisionJournalTests.EveryKind[2]], TestContext.Current.CancellationToken);

        late.ShouldBeNull();
        (await _journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Decisions.ShouldBe([DecisionJournalTests.EveryKind[0], DecisionJournalTests.EveryKind[1]]);
    }

    [Fact]
    public async Task A_decision_line_without_what_its_kind_needs_is_a_broken_file()
    {
        Directory.CreateDirectory(Path.Combine(_root, "run-1"));
        await File.WriteAllTextAsync(Path.Combine(_root, "run-1", "decisions.jsonl"), """{"seq":1,"at":"2026-11-10T14:00:00+00:00","slot":"player1","kind":"Speed","creature":2,"pass":false}""" + "\n", TestContext.Current.CancellationToken);

        var broken = await Should.ThrowAsync<InvalidDataException>(() => _journal.ReadAsync("run-1", TestContext.Current.CancellationToken));

        broken.Message.ShouldContain("line 1");
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData("a/b")]
    [InlineData("..")]
    public async Task A_run_name_that_is_not_one_directory_under_the_root_is_refused(string run)
    {
        await Should.ThrowAsync<ArgumentException>(() => _journal.ReadAsync(run, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => _journal.AppendAsync(run, JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken));
    }

    private sealed class At(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
