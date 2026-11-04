using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every friend store promises, played against each adapter.</summary>
public abstract class FriendStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IFriendStore> StoreAsync();

    private static FriendLink Link(int day) => new(AccountId.New(), new DateTimeOffset(2026, 11, day, 20, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task A_list_reads_back_as_it_was_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var list = new FriendList([Link(1), Link(2)], [Link(3)], [Link(4)]);

        await store.SaveAsync(account, list, Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBe(list);

        var smaller = list with { Incoming = [] };
        await store.SaveAsync(account, smaller, Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBe(smaller);
    }

    [Fact]
    public async Task An_account_with_no_list_reads_as_empty_and_an_empty_save_leaves_none()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.ListAsync(account, Cancel)).ShouldBe(FriendList.Empty);

        await store.SaveAsync(account, new FriendList([Link(1)], [], []), Cancel);
        await store.SaveAsync(account, FriendList.Empty, Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBe(FriendList.Empty);
    }

    [Fact]
    public async Task Forgetting_an_account_removes_its_list_and_forgetting_twice_is_fine()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, new FriendList([Link(1)], [], []), Cancel);

        await store.ForgetAsync(account, Cancel);
        await store.ForgetAsync(account, Cancel);

        (await store.ListAsync(account, Cancel)).ShouldBe(FriendList.Empty);
    }
}
