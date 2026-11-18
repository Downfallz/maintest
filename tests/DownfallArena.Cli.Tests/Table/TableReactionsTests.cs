using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>What a table keeps of its reactions and when a seat may send the next one (ADR 0173).</summary>
public sealed class TableReactionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_seat_reacts_again_once_the_cooldown_has_passed()
    {
        var clock = new Clock(Start);
        var reactions = new TableReactions(clock);
        await reactions.SendAsync("player1", "hello", Cancel);
        clock.Now = Start + TableReactions.Cooldown;

        var sent = await reactions.SendAsync("player1", "thanks", Cancel);

        sent.Refusal.ShouldBeNull();
        sent.Reaction.ShouldNotBeNull().Sequence.ShouldBe(2);
    }

    [Fact]
    public async Task The_table_keeps_only_its_last_reactions()
    {
        var clock = new Clock(Start);
        var reactions = new TableReactions(clock);
        for (var sent = 0; sent < TableReactions.Kept + 3; sent++)
        {
            clock.Now = Start + (TableReactions.Cooldown * sent);
            await reactions.SendAsync("player1", "hello", Cancel);
        }

        var recent = await reactions.RecentAsync(Cancel);

        recent.Count.ShouldBe(TableReactions.Kept);
        recent[0].Sequence.ShouldBe(4);
        recent[^1].Sequence.ShouldBe(TableReactions.Kept + 3);
    }

    [Fact]
    public async Task A_reaction_another_process_kept_first_is_read_and_the_new_one_goes_after_it()
    {
        var keeper = new Keeper();
        var reactions = new TableReactions(new Clock(Start), keeper);
        await reactions.RecentAsync(Cancel);
        keeper.Elsewhere(new TableReaction(1, "player2", "hello", Start));

        var sent = await reactions.SendAsync("player1", "good-luck", Cancel);

        sent.Reaction.ShouldNotBeNull().Sequence.ShouldBe(2);
        keeper.Kept.Select(one => one.Reaction).ShouldBe(["hello", "good-luck"]);
    }

    [Fact]
    public async Task A_reaction_sent_while_the_run_is_deleted_does_not_bring_its_file_back()
    {
        var keeper = new Keeper();
        keeper.Elsewhere(new TableReaction(1, "player2", "hello", Start));
        var reactions = new TableReactions(new Clock(Start), keeper);
        await reactions.RecentAsync(Cancel);
        keeper.Delete();

        var sent = await reactions.SendAsync("player1", "good-luck", Cancel);

        sent.Refusal.ShouldBe("Reaction.Gone");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_reaction_sent_after_the_run_was_deleted_and_read_again_does_not_bring_its_file_back()
    {
        var keeper = new Keeper();
        keeper.Elsewhere(new TableReaction(1, "player2", "hello", Start));
        var reactions = new TableReactions(new Clock(Start), keeper);
        await reactions.RecentAsync(Cancel);
        keeper.Delete();
        reactions.Stale();
        await reactions.RecentAsync(Cancel);

        var sent = await reactions.SendAsync("player1", "good-luck", Cancel);

        sent.Refusal.ShouldBe("Reaction.Gone");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_first_reaction_after_the_run_was_deleted_does_not_bring_a_file_of_it_back()
    {
        var keeper = new Keeper { Recorded = RecordPresence.Gone };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Refusal.ShouldBe("Reaction.Gone");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_first_reaction_written_while_the_run_is_deleted_is_taken_back_with_it()
    {
        var keeper = new Keeper { DeletedAfterWrite = true };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Refusal.ShouldBe("Reaction.Gone");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_first_reaction_another_process_wrote_over_while_the_run_is_deleted_is_taken_back_too()
    {
        var keeper = new Keeper { DeletedAfterWrite = true, WrittenOverAfterWrite = true };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Refusal.ShouldBe("Reaction.Gone");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_first_reaction_is_kept_when_the_second_look_at_the_record_fails()
    {
        var keeper = new Keeper { RecordUnreadableOnceWritten = true };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Reaction.ShouldNotBeNull().Reaction.ShouldBe("hello");
        keeper.Exists.ShouldBeTrue();
    }

    [Fact]
    public async Task A_first_reaction_while_no_record_was_ever_seen_is_busy_not_gone()
    {
        var keeper = new Keeper { Recorded = RecordPresence.Unknown };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Refusal.ShouldBe("Reaction.Busy");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_first_reaction_whose_record_cannot_be_told_from_a_deleted_one_after_the_write_is_taken_back_as_busy()
    {
        var keeper = new Keeper { UnknownAfterWrite = true };
        var reactions = new TableReactions(new Clock(Start), keeper);

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Refusal.ShouldBe("Reaction.Busy");
        keeper.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task A_reaction_refused_for_want_of_a_record_is_sent_once_the_record_is_written()
    {
        var keeper = new Keeper { Recorded = RecordPresence.Unknown };
        var reactions = new TableReactions(new Clock(Start), keeper);
        await reactions.SendAsync("player1", "hello", Cancel);
        keeper.Recorded = RecordPresence.Kept;

        var sent = await reactions.SendAsync("player1", "hello", Cancel);

        sent.Reaction.ShouldNotBeNull().Reaction.ShouldBe("hello");
        keeper.Exists.ShouldBeTrue();
    }

    [Fact]
    public async Task A_kept_reaction_the_set_no_longer_offers_is_not_served()
    {
        var keeper = new Keeper();
        keeper.Elsewhere(new TableReaction(1, "player2", "retired", Start));
        var reactions = new TableReactions(new Clock(Start), keeper);

        var recent = await reactions.RecentAsync(Cancel);

        recent.ShouldBeEmpty();
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A record kept in memory, whose tag moves with every write, the way a blob's does.</summary>
    private sealed class Keeper : IReactionKeeper
    {
        private int _version;

        public IReadOnlyList<TableReaction> Kept { get; private set; } = [];

        public void Elsewhere(TableReaction reaction)
        {
            Kept = [.. Kept, reaction];
            _version++;
        }

        public bool Exists => _version > 0;

        /// <summary>Whether the table's record is still kept: false once the operator deleted the run.</summary>
        public RecordPresence Recorded { get; set; } = RecordPresence.Kept;

        public Task<RecordPresence> RecordedAsync(CancellationToken cancellationToken = default) =>
            RecordUnreadableOnceWritten && _version > 0 ? Task.FromException<RecordPresence>(new IOException("The store is out.")) : Task.FromResult(Recorded);

        /// <summary>A storage failure answers the second look at the record, once the file is written.</summary>
        public bool RecordUnreadableOnceWritten { get; init; }

        /// <summary>Another process writes over the file between this one's write and its second look at the record.</summary>
        public bool WrittenOverAfterWrite { get; init; }

        /// <summary>The operator deletes the run right after the first write, as a delete in another process does.</summary>
        public bool DeletedAfterWrite { get; init; }

        /// <summary>The record reads as never seen right after the first write, as one whose last write failed does.</summary>
        public bool UnknownAfterWrite { get; init; }

        public Task<bool> ForgetReactionsIfAsync(string expected, CancellationToken cancellationToken = default)
        {
            if (_version == 0 || _version.ToString(System.Globalization.CultureInfo.InvariantCulture) != expected)
            {
                return Task.FromResult(false);
            }

            Delete();
            return Task.FromResult(true);
        }

        /// <summary>The operator deletes the run: the file is gone, and a write expecting its tag is refused.</summary>
        public void Delete()
        {
            Kept = [];
            _version = 0;
        }

        public Task<(IReadOnlyList<TableReaction> Items, string? Tag)> ReactionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((Kept, _version == 0 ? null : (string?)_version.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public Task<string?> WriteReactionsIfAsync(IReadOnlyList<TableReaction> items, string? expected, CancellationToken cancellationToken = default)
        {
            var current = _version == 0 ? null : _version.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (current != expected)
            {
                return Task.FromResult<string?>(null);
            }

            Kept = items;
            _version++;
            var tag = _version.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (DeletedAfterWrite && current is null)
            {
                Recorded = RecordPresence.Gone;
            }

            if (UnknownAfterWrite && current is null)
            {
                Recorded = RecordPresence.Unknown;
            }

            if (WrittenOverAfterWrite && current is null)
            {
                _version++;
            }

            return Task.FromResult<string?>(tag);
        }
    }
}
