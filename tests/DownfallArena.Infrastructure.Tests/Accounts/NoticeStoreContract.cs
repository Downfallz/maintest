using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every notice store promises (ADR 0201), played against each adapter.</summary>
public abstract class NoticeStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<INoticeStore> StoreAsync();

    private static NoticeEntry Entry(long id, NoticeKind kind = NoticeKind.MatchOver, bool read = false) =>
        new(id, kind, "Victory", "You won against bastien.", "/play?table=t1", $"match:t{id}", new DateTimeOffset(2026, 11, 22, 20, 0, 0, TimeSpan.Zero).AddMinutes(id), read);

    [Fact]
    public async Task A_log_reads_back_as_it_was_written_with_every_field_and_kind()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        NoticeEntry[] entries = [Entry(3, NoticeKind.Left, read: true), Entry(2, NoticeKind.SeatTaken), Entry(1, NoticeKind.FriendAccepted)];

        (await store.WriteAsync(account, entries, null, Cancel)).ShouldBeTrue();

        var read = await store.ReadAsync(account, Cancel);
        read.Entries.ShouldBe(entries);
        read.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task An_account_with_no_log_reads_none_and_no_version_and_an_empty_write_leaves_none()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.ReadAsync(account, Cancel)).ShouldBe(new NoticeLogRead([], null));

        await store.WriteAsync(account, [Entry(1)], null, Cancel);
        (await store.WriteAsync(account, [], (await store.ReadAsync(account, Cancel)).Version, Cancel)).ShouldBeTrue();
        (await store.ReadAsync(account, Cancel)).Version.ShouldBeNull();
    }

    [Fact]
    public async Task The_accounts_with_a_log_are_listed_and_one_whose_log_went_is_not()
    {
        var store = await StoreAsync();
        var kept = AccountId.New();
        var emptied = AccountId.New();
        var forgotten = AccountId.New();
        foreach (var account in new[] { kept, emptied, forgotten })
        {
            await store.WriteAsync(account, [Entry(1)], null, Cancel);
        }

        await store.WriteAsync(emptied, [], (await store.ReadAsync(emptied, Cancel)).Version, Cancel);
        await store.ForgetAsync(forgotten, Cancel);

        var listed = await store.AccountsAsync(Cancel);
        listed.ShouldContain(kept);
        listed.ShouldNotContain(emptied);
        listed.ShouldNotContain(forgotten);
    }

    [Fact]
    public async Task A_write_over_a_version_another_write_replaced_is_refused_and_leaves_that_write()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.WriteAsync(account, [Entry(1)], null, Cancel);
        var stale = (await store.ReadAsync(account, Cancel)).Version;
        await store.WriteAsync(account, [Entry(2), Entry(1)], stale, Cancel);

        var written = await store.WriteAsync(account, [Entry(3), Entry(1)], stale, Cancel);

        written.ShouldBeFalse();
        (await store.ReadAsync(account, Cancel)).Entries.ShouldBe([Entry(2), Entry(1)]);
    }

    [Fact]
    public async Task A_first_write_is_refused_once_another_one_made_the_log()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.WriteAsync(account, [Entry(1)], null, Cancel);

        var written = await store.WriteAsync(account, [Entry(2)], null, Cancel);

        written.ShouldBeFalse();
        (await store.ReadAsync(account, Cancel)).Entries.ShouldBe([Entry(1)]);
    }

    [Fact]
    public async Task Forgetting_an_account_removes_its_log_and_forgetting_twice_is_fine()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.WriteAsync(account, [Entry(1)], null, Cancel);

        await store.ForgetAsync(account, Cancel);
        await store.ForgetAsync(account, Cancel);

        (await store.ReadAsync(account, Cancel)).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task One_account_s_log_is_not_another_s()
    {
        var store = await StoreAsync();
        var mark = AccountId.New();
        var bastien = AccountId.New();

        await store.WriteAsync(mark, [Entry(1)], null, Cancel);

        (await store.ReadAsync(bastien, Cancel)).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Emptying_a_log_over_a_version_another_write_replaced_is_refused_and_leaves_that_write()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.WriteAsync(account, [Entry(1)], null, Cancel);
        var stale = (await store.ReadAsync(account, Cancel)).Version;
        await store.WriteAsync(account, [Entry(2), Entry(1)], stale, Cancel);

        var written = await store.WriteAsync(account, [], stale, Cancel);

        written.ShouldBeFalse();
        (await store.ReadAsync(account, Cancel)).Entries.ShouldBe([Entry(2), Entry(1)]);
    }

    [Fact]
    public async Task Emptying_a_log_read_as_absent_is_refused_once_another_write_made_it()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.WriteAsync(account, [Entry(1)], null, Cancel);

        var written = await store.WriteAsync(account, [], null, Cancel);

        written.ShouldBeFalse();
        (await store.ReadAsync(account, Cancel)).Entries.ShouldBe([Entry(1)]);
    }

    [Fact]
    public async Task Forgetting_an_account_on_a_store_that_never_wrote_anything_is_fine()
    {
        var store = await StoreAsync();

        await store.ForgetAsync(AccountId.New(), Cancel);

        (await store.ReadAsync(AccountId.New(), Cancel)).Entries.ShouldBeEmpty();
    }
}
