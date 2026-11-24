using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>What every store of the notices' counts promises (N6), played against each adapter.</summary>
public abstract class NoticeCountStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<INoticeCountStore> StoreAsync();

    /// <summary>
    /// A second store over the same days, as another process opens it: one that kept nothing of what
    /// <paramref name="store" /> read (#1232). The same store for an adapter with one copy in memory.
    /// </summary>
    protected virtual Task<INoticeCountStore> OtherStoreAsync(INoticeCountStore store) => Task.FromResult(store);

    private static readonly DateOnly Monday = new(2026, 11, 23);

    [Fact]
    public async Task A_day_reads_back_as_it_was_written_with_its_version()
    {
        var store = await StoreAsync();
        NoticeCount[] counts = [new("friend", 1, 2, 3), new("turn", 40, 0, 1)];

        (await store.WriteAsync(Monday, counts, null, Cancel)).ShouldBeTrue();

        var read = await store.ReadAsync(Monday, Cancel);
        read.Counts.ShouldBe(counts);
        read.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_day_nothing_was_counted_on_reads_none_and_no_version()
    {
        var store = await StoreAsync();

        (await store.ReadAsync(Monday, Cancel)).ShouldBe(new NoticeCountRead([], null));
    }

    [Fact]
    public async Task A_write_over_a_version_another_write_replaced_is_refused()
    {
        var store = await StoreAsync();
        await store.WriteAsync(Monday, [new("turn", 1, 0, 0)], null, Cancel);
        var read = await store.ReadAsync(Monday, Cancel);
        (await store.WriteAsync(Monday, [new("turn", 2, 0, 0)], read.Version, Cancel)).ShouldBeTrue();

        (await store.WriteAsync(Monday, [new("turn", 9, 0, 0)], read.Version, Cancel)).ShouldBeFalse();
        (await store.WriteAsync(Monday, [new("turn", 9, 0, 0)], null, Cancel)).ShouldBeFalse("a first write is refused once the day has a document");

        (await store.ReadAsync(Monday, Cancel)).Counts.ShouldBe([new NoticeCount("turn", 2, 0, 0)]);
    }

    [Fact]
    public async Task Forgetting_before_a_day_lets_go_of_the_days_before_it_and_keeps_that_day_and_after()
    {
        var store = await StoreAsync();
        foreach (var back in new[] { 0, 6, 7, 30 })
        {
            await store.WriteAsync(Monday.AddDays(-back), [new("turn", back + 1, 0, 0)], null, Cancel);
        }

        await store.ForgetBeforeAsync(Monday.AddDays(-6), Cancel);

        (await store.ReadAsync(Monday, Cancel)).Counts.ShouldNotBeEmpty();
        (await store.ReadAsync(Monday.AddDays(-6), Cancel)).Counts.ShouldNotBeEmpty();
        (await store.ReadAsync(Monday.AddDays(-7), Cancel)).Version.ShouldBeNull();
        (await store.ReadAsync(Monday.AddDays(-30), Cancel)).Version.ShouldBeNull();
    }

    /// <summary>The text this store kept of its read does not let its write past another store's since (#1232).</summary>
    [Fact]
    public async Task A_write_over_the_version_read_is_refused_once_another_store_wrote_since()
    {
        var store = await StoreAsync();
        var other = await OtherStoreAsync(store);
        await store.WriteAsync(Monday, [new("turn", 1, 0, 0)], null, Cancel);
        var read = await store.ReadAsync(Monday, Cancel);
        (await other.WriteAsync(Monday, [new("turn", 2, 0, 0)], (await other.ReadAsync(Monday, Cancel)).Version, Cancel)).ShouldBeTrue();

        var written = await store.WriteAsync(Monday, [new("turn", 9, 0, 0)], read.Version, Cancel);

        written.ShouldBeFalse();
        (await store.ReadAsync(Monday, Cancel)).Counts.ShouldBe([new NoticeCount("turn", 2, 0, 0)]);
    }
}
