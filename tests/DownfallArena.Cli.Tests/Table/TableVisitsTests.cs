using System.Globalization;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>What a table keeps of the last thing each seat was shown, and when (ADR 0194).</summary>
public sealed class TableVisitsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 20, 0, 0, TimeSpan.Zero);

    private const string Holder = "seat-key";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_seat_shown_further_is_kept_with_when_it_was_shown()
    {
        var visits = new TableVisits(new Clock(Start));

        var kept = await visits.ShownAsync("player1", Holder, 7, Cancel);

        kept.ShouldBe(new LastVisit(7, Start, Holder));
        (await visits.LastAsync("player1", Holder, Cancel)).ShouldBe(new LastVisit(7, Start, Holder));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public async Task A_sequence_not_larger_than_the_one_kept_changes_nothing(long sequence)
    {
        var clock = new Clock(Start);
        var visits = new TableVisits(clock);
        await visits.ShownAsync("player1", Holder, 9, Cancel);
        clock.Now = Start + TimeSpan.FromHours(2);

        var kept = await visits.ShownAsync("player1", Holder, sequence, Cancel);

        kept.ShouldBe(new LastVisit(9, Start, Holder));
    }

    [Fact]
    public async Task Each_seat_has_its_own_last_visit()
    {
        var visits = new TableVisits(new Clock(Start));

        await visits.ShownAsync("player1", Holder, 12, Cancel);

        (await visits.LastAsync("player2", Holder, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_larger_mark_another_process_kept_first_is_kept()
    {
        var keeper = new Keeper();
        keeper.Meanwhile("player1", new LastVisit(20, Start, Holder));
        var visits = new TableVisits(new Clock(Start + TimeSpan.FromMinutes(1)), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 15, Cancel);

        kept.ShouldBe(new LastVisit(20, Start, Holder));
        keeper.Kept["player1"].ShouldBe(new LastVisit(20, Start, Holder));
    }

    [Fact]
    public async Task A_smaller_mark_another_process_kept_first_is_read_again_and_written_over()
    {
        var keeper = new Keeper();
        keeper.Meanwhile("player1", new LastVisit(10, Start, Holder));
        var later = Start + TimeSpan.FromMinutes(1);
        var visits = new TableVisits(new Clock(later), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 15, Cancel);

        kept.ShouldBe(new LastVisit(15, later, Holder));
        keeper.Kept["player1"].ShouldBe(new LastVisit(15, later, Holder));
    }

    [Fact]
    public async Task A_mark_another_process_keeps_moving_is_given_up_on()
    {
        var keeper = new Keeper { AlwaysMoved = true };
        var visits = new TableVisits(new Clock(Start), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 3, Cancel);

        kept.ShouldBeNull();
    }

    [Fact]
    public async Task A_kept_mark_is_read_from_the_record_on_every_read()
    {
        var keeper = new Keeper();
        var visits = new TableVisits(new Clock(Start), keeper);
        await visits.ShownAsync("player1", Holder, 3, Cancel);

        keeper.Elsewhere("player1", new LastVisit(8, Start, Holder));

        (await visits.LastAsync("player1", Holder, Cancel)).ShouldBe(new LastVisit(8, Start, Holder));
    }

    [Fact]
    public async Task A_mark_kept_under_the_seats_previous_key_is_no_visit_of_its_new_player()
    {
        var visits = new TableVisits(new Clock(Start));
        await visits.ShownAsync("player1", Holder, 9, Cancel);

        var last = await visits.LastAsync("player1", "next-key", Cancel);

        last.ShouldBeNull();
    }

    [Fact]
    public async Task The_seats_new_player_writes_over_the_previous_players_mark_whatever_its_sequence()
    {
        var visits = new TableVisits(new Clock(Start));
        await visits.ShownAsync("player1", Holder, 9, Cancel);

        var kept = await visits.ShownAsync("player1", "next-key", 2, Cancel);

        kept.ShouldBe(new LastVisit(2, Start, "next-key"));
    }

    [Fact]
    public async Task A_mark_whose_record_was_deleted_meanwhile_is_not_written_back()
    {
        var keeper = new Keeper();
        keeper.Elsewhere("player1", new LastVisit(3, Start, Holder));
        keeper.DeletedMeanwhile("player1");
        var visits = new TableVisits(new Clock(Start), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 8, Cancel);

        kept.ShouldBeNull();
        keeper.Kept.ShouldNotContainKey("player1");
    }

    [Fact]
    public async Task A_first_mark_after_the_run_was_deleted_does_not_bring_a_file_of_it_back()
    {
        var keeper = new Keeper { Recorded = false };
        var visits = new TableVisits(new Clock(Start), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 8, Cancel);

        kept.ShouldBeNull();
        keeper.Kept.ShouldNotContainKey("player1");
    }

    [Fact]
    public async Task A_first_mark_written_while_the_run_is_deleted_is_taken_back_with_it()
    {
        var keeper = new Keeper { DeletedAfterFirstWrite = true };
        var visits = new TableVisits(new Clock(Start), keeper);

        var kept = await visits.ShownAsync("player1", Holder, 8, Cancel);

        kept.ShouldBeNull();
        keeper.Kept.ShouldNotContainKey("player1");
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A record per seat kept in memory, whose tag moves with every write, the way a blob's does.</summary>
    private sealed class Keeper : IVisitKeeper
    {
        private readonly Dictionary<string, int> _versions = [];
        private readonly Dictionary<string, LastVisit?> _meanwhile = [];

        public Dictionary<string, LastVisit> Kept { get; } = [];

        /// <summary>Whether the table's record is still kept: false once the operator deleted the run.</summary>
        public bool Recorded { get; set; } = true;

        /// <summary>The operator deletes the run right after a seat's first write, as a delete in another process does.</summary>
        public bool DeletedAfterFirstWrite { get; init; }

        public Task<bool> ForgetVisitIfAsync(string seat, string expected, CancellationToken cancellationToken = default)
        {
            if (Tag(seat) != expected)
            {
                return Task.FromResult(false);
            }

            Kept.Remove(seat);
            _versions.Remove(seat);
            return Task.FromResult(true);
        }

        public Task<bool> RecordedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Recorded);

        /// <summary>Every write is refused, as if another process wrote first each time.</summary>
        public bool AlwaysMoved { get; init; }

        /// <summary>Another process writes this mark between this one's read and its write.</summary>
        public void Meanwhile(string seat, LastVisit visit) => _meanwhile[seat] = visit;

        /// <summary>The run is deleted between this one's read and its write, as the operator deleting a session does.</summary>
        public void DeletedMeanwhile(string seat) => _meanwhile[seat] = null;

        public void Elsewhere(string seat, LastVisit visit)
        {
            Kept[seat] = visit;
            _versions[seat] = Version(seat) + 1;
        }

        public Task<(LastVisit? Visit, string? Tag)> VisitAsync(string seat, CancellationToken cancellationToken = default) =>
            Task.FromResult((Kept.TryGetValue(seat, out var visit) ? visit : null, Tag(seat)));

        public Task<string?> WriteVisitIfAsync(string seat, LastVisit visit, string? expected, CancellationToken cancellationToken = default)
        {
            if (_meanwhile.Remove(seat, out var first))
            {
                if (first is null)
                {
                    Kept.Remove(seat);
                    _versions.Remove(seat);
                }
                else
                {
                    Elsewhere(seat, first);
                }
            }

            if (AlwaysMoved || Tag(seat) != expected)
            {
                return Task.FromResult<string?>(null);
            }

            var firstWrite = Tag(seat) is null;
            Elsewhere(seat, visit);
            if (DeletedAfterFirstWrite && firstWrite)
            {
                Recorded = false;
            }

            return Task.FromResult(Tag(seat));
        }

        private int Version(string seat) => _versions.TryGetValue(seat, out var version) ? version : 0;

        private string? Tag(string seat) => Version(seat) == 0 ? null : Version(seat).ToString(CultureInfo.InvariantCulture);
    }
}
