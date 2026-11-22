using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>
/// An account's notice log (ADR 0201): the news the bell keeps, the 100 latest of the last 90 days, a tag replacing
/// its earlier entry, read up to an id, and two writers that both keep their entry.
/// </summary>
public sealed class AccountNoticeLogTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly AccountId _mark = AccountId.New();
    private readonly SteppedClock _clock = new(new DateTimeOffset(2026, 11, 22, 20, 0, 0, TimeSpan.Zero));
    private readonly VersionedNotices _store = new();
    private readonly AccountNoticeLog _log;

    public AccountNoticeLogTests()
    {
        _log = new AccountNoticeLog(_store, _clock);
    }

    private static Notice Over(string table, string title = "Victory") => new(NoticeKind.MatchOver, title, "You won against bastien.", $"/play?table={table}", $"match:{table}");

    [Fact]
    public async Task A_notice_is_kept_unread_stamped_now_and_listed_newest_first()
    {
        await _log.AddAsync(_mark, Over("t1"), Cancel);
        _clock.Advance(TimeSpan.FromMinutes(1));

        var added = await _log.AddAsync(_mark, Over("t2"), Cancel);

        added.Read.ShouldBeFalse();
        added.At.ShouldBe(_clock.GetUtcNow());
        (await _log.ListAsync(_mark, Cancel)).Select(entry => entry.Tag).ShouldBe(["match:t2", "match:t1"]);
    }

    [Fact]
    public async Task Each_entry_gets_an_id_past_the_ones_before_even_within_one_millisecond()
    {
        var first = await _log.AddAsync(_mark, Over("t1"), Cancel);

        var second = await _log.AddAsync(_mark, Over("t2"), Cancel);

        second.Id.ShouldBeGreaterThan(first.Id);
    }

    [Fact]
    public async Task A_notice_with_the_tag_of_an_entry_replaces_it_as_the_newest_and_unread()
    {
        var first = await _log.AddAsync(_mark, Over("t1", "Match voided"), Cancel);
        await _log.MarkReadAsync(_mark, first.Id, Cancel);
        await _log.AddAsync(_mark, Over("t2"), Cancel);

        await _log.AddAsync(_mark, Over("t1", "Victory"), Cancel);

        var listed = await _log.ListAsync(_mark, Cancel);
        listed.Select(entry => entry.Tag).ShouldBe(["match:t1", "match:t2"]);
        listed[0].Title.ShouldBe("Victory");
        listed[0].Read.ShouldBeFalse();
    }

    [Fact]
    public async Task The_log_keeps_the_hundred_latest_entries()
    {
        for (var index = 0; index <= AccountNoticeLog.MostKept; index++)
        {
            await _log.AddAsync(_mark, Over($"t{index}"), Cancel);
        }

        var listed = await _log.ListAsync(_mark, Cancel);

        listed.Count.ShouldBe(AccountNoticeLog.MostKept);
        listed[^1].Tag.ShouldBe("match:t1", "the oldest went");
    }

    [Fact]
    public async Task An_entry_older_than_ninety_days_is_not_listed_and_goes_at_the_next_write()
    {
        await _log.AddAsync(_mark, Over("old"), Cancel);
        _clock.Advance(AccountNoticeLog.KeptFor + TimeSpan.FromMinutes(1));

        (await _log.ListAsync(_mark, Cancel)).ShouldBeEmpty();

        await _log.AddAsync(_mark, Over("new"), Cancel);
        (await _store.ReadAsync(_mark, Cancel)).Entries.Select(entry => entry.Tag).ShouldBe(["match:new"]);
    }

    [Fact]
    public async Task Reading_up_to_an_id_marks_that_entry_and_the_older_ones_and_leaves_the_newer()
    {
        await _log.AddAsync(_mark, Over("t1"), Cancel);
        var middle = await _log.AddAsync(_mark, Over("t2"), Cancel);
        await _log.AddAsync(_mark, Over("t3"), Cancel);

        var marked = await _log.MarkReadAsync(_mark, middle.Id, Cancel);

        marked.ShouldBe(2);
        (await _log.ListAsync(_mark, Cancel)).Select(entry => entry.Read).ShouldBe([false, true, true]);
    }

    [Fact]
    public async Task Reading_what_is_read_already_writes_nothing()
    {
        var entry = await _log.AddAsync(_mark, Over("t1"), Cancel);
        await _log.MarkReadAsync(_mark, entry.Id, Cancel);
        var writes = _store.Writes;

        var marked = await _log.MarkReadAsync(_mark, entry.Id, Cancel);

        marked.ShouldBe(0);
        _store.Writes.ShouldBe(writes);
    }

    [Fact]
    public async Task Two_writers_at_once_both_keep_their_entry()
    {
        // The worker's entry lands between this process's read and its write: the write is refused, read again, kept.
        _store.BeforeNextWrite = () => new AccountNoticeLog(_store, _clock).AddAsync(_mark, Over("worker"), Cancel);

        await _log.AddAsync(_mark, Over("api"), Cancel);

        (await _log.ListAsync(_mark, Cancel)).Select(entry => entry.Tag).ShouldBe(["match:api", "match:worker"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_change_that_loses_every_try_to_others_throws_rather_than_writes_over_them()
    {
        _store.RefuseEveryWrite = true;

        await Should.ThrowAsync<IOException>(() => _log.AddAsync(_mark, Over("t1"), Cancel));

        _store.Writes.ShouldBe(AccountNoticeLog.Tries);
    }

    [Fact]
    public async Task Forgetting_the_account_forgets_its_log()
    {
        await _log.AddAsync(_mark, Over("t1"), Cancel);

        await _log.ForgetAsync(_mark, Cancel);

        (await _log.ListAsync(_mark, Cancel)).ShouldBeEmpty();
    }

    private sealed class SteppedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>A log in memory that refuses a write over a version another write replaced, as the real stores do.</summary>
    private sealed class VersionedNotices : INoticeStore
    {
        private readonly Dictionary<AccountId, (IReadOnlyList<NoticeEntry> Entries, string Version)> _held = [];
        private long _written;

        public Func<Task>? BeforeNextWrite { get; set; }

        public bool RefuseEveryWrite { get; set; }

        public int Writes { get; private set; }

        public Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var held) ? new NoticeLogRead(held.Entries, held.Version) : new NoticeLogRead([], null));

        public async Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default)
        {
            if (BeforeNextWrite is { } before)
            {
                BeforeNextWrite = null;
                await before();
            }

            Writes++;
            if (RefuseEveryWrite || (_held.TryGetValue(account, out var held) ? held.Version : null) != version)
            {
                return false;
            }

            _held[account] = ([.. entries], $"{++_written}");
            return true;
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
        {
            _held.Remove(account);
            return Task.CompletedTask;
        }
    }
}
