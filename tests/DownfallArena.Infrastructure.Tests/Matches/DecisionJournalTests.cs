using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// What every decision journal does, whatever keeps it (ADR 0159): it reads back what was written, in order,
/// and takes a line only from a writer that knows where it ends.
/// </summary>
public sealed class DecisionJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-journal-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>An entry of each kind: every decision a seat makes, with both seats and both shapes of a pick, a seat changing hands, a concession.</summary>
    internal static IReadOnlyList<IJournalEntry> EveryKind { get; } =
    [
        Decided(PlayerSlot.Player1, PlayerDecision.Buy(CreatureId.From(1), TierId.Parse("tier:guard:v1"))),
        Decided(PlayerSlot.Player2, PlayerDecision.Pass),
        Decided(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(CreatureId.From(2), Speed.Quick)),
        Decided(PlayerSlot.Player2, PlayerDecision.OrderTies([CreatureId.From(4), CreatureId.From(3)])),
        Decided(PlayerSlot.Player1, PlayerDecision.DeclareIntent(CreatureId.From(1), SpellId.Parse("spell:strike:v1"))),
        Decided(PlayerSlot.Player2, PlayerDecision.BindTargets([CreatureId.From(1), CreatureId.From(2)])),
        Decided(PlayerSlot.Player1, PlayerDecision.BindTargets([])),
        Decided(PlayerSlot.Player2, PlayerDecision.Act(CreatureId.From(3), SpellId.Parse("spell:guard:v1"), [CreatureId.From(3)])),
        new SeatChangeEntry(PlayerSlot.Player2, "greedy", 3, Reached: 1),
        new ConcessionEntry(PlayerSlot.Player1),
    ];

    private static DecisionEntry Decided(PlayerSlot slot, PlayerDecision decision) => new(new SeatDecision(slot, decision));

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task A_journal_nobody_wrote_is_empty_and_ends_at_the_start(string kept)
    {
        var reading = await Journal(kept).ReadAsync("run-1", TestContext.Current.CancellationToken);

        reading.Entries.ShouldBeEmpty();
        reading.Position.ShouldBe(JournalPosition.Start);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task Where_a_journal_ends_is_read_without_reading_it_and_is_where_the_last_append_left_it(string kept)
    {
        var journal = Journal(kept);
        var nothing = await journal.EndAsync("run-1", TestContext.Current.CancellationToken);
        var written = (await journal.AppendAsync("run-1", JournalPosition.Start, [.. EveryKind.Take(3)], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var end = await journal.EndAsync("run-1", TestContext.Current.CancellationToken);

        nothing.ShouldBe(0);
        end.ShouldBe(written.Mark);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task Entries_of_every_kind_are_read_back_as_they_were_written_in_order(string kept)
    {
        var journal = Journal(kept);

        var end = await journal.AppendAsync("run-1", JournalPosition.Start, EveryKind, TestContext.Current.CancellationToken);

        var reading = await journal.ReadAsync("run-1", TestContext.Current.CancellationToken);
        reading.Entries.ShouldBe(EveryKind);
        reading.Position.ShouldBe(end);
        reading.Position.Lines.ShouldBe(EveryKind.Count);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task A_second_append_goes_after_the_first_when_it_names_where_the_first_ended(string kept)
    {
        var journal = Journal(kept);
        var first = (await journal.AppendAsync("run-1", JournalPosition.Start, [EveryKind[0], EveryKind[1]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var second = await journal.AppendAsync("run-1", first, [EveryKind[2]], TestContext.Current.CancellationToken);

        second.ShouldNotBeNull().Lines.ShouldBe(3);
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Entries.ShouldBe([EveryKind[0], EveryKind[1], EveryKind[2]]);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task A_writer_that_read_before_another_wrote_is_refused_and_writes_nothing(string kept)
    {
        var journal = Journal(kept);
        var read = (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Position;
        await journal.AppendAsync("run-1", read, [EveryKind[0]], TestContext.Current.CancellationToken);

        var late = await journal.AppendAsync("run-1", read, [EveryKind[1]], TestContext.Current.CancellationToken);

        late.ShouldBeNull();
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Entries.ShouldBe([EveryKind[0]]);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task Each_run_keeps_its_own_journal(string kept)
    {
        var journal = Journal(kept);

        await journal.AppendAsync("run-1", JournalPosition.Start, [EveryKind[0]], TestContext.Current.CancellationToken);
        var other = await journal.AppendAsync("run-2", JournalPosition.Start, [EveryKind[1]], TestContext.Current.CancellationToken);

        other.ShouldNotBeNull();
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Entries.ShouldBe([EveryKind[0]]);
        (await journal.ReadAsync("run-2", TestContext.Current.CancellationToken)).Entries.ShouldBe([EveryKind[1]]);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("file")]
    public async Task Appending_nothing_leaves_the_end_where_it_was(string kept)
    {
        var journal = Journal(kept);
        var end = (await journal.AppendAsync("run-1", JournalPosition.Start, [EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var same = await journal.AppendAsync("run-1", end, [], TestContext.Current.CancellationToken);
        var stale = await journal.AppendAsync("run-1", JournalPosition.Start, [], TestContext.Current.CancellationToken);

        same.ShouldBe(end);
        stale.ShouldBeNull();
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Position.ShouldBe(end);
    }

    private IDecisionJournal Journal(string kept) =>
        kept == "file" ? new FileDecisionJournal(_root, TimeProvider.System) : new InMemoryDecisionJournal();
}
