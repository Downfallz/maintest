using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>
/// Friendships are mutual (ADR 0129): a request on both lists until the other answers, a friendship on both once
/// they do, nothing on either once somebody declines, withdraws or removes. Asking back accepts; nobody is asked
/// twice; a deleted account is on nobody's list.
/// </summary>
public sealed class FriendsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly MemoryAccountStore _accounts = new();
    private readonly InMemoryFriends _store = new();
    private readonly FixedTimeProvider _clock = new(FixedTimeProvider.Default);
    private readonly Friends _friends;
    private readonly AccountId _mark;
    private readonly AccountId _bastien;

    public FriendsTests()
    {
        _friends = new Friends(_store, _accounts, _clock);
        _mark = Register("MarkF");
        _bastien = Register("bastien");
    }

    private AccountId Register(string name)
    {
        var id = AccountId.New();
        var username = Username.Create(name).Value;
        _accounts.Accounts[id] = new Account(id, new ExternalIdentity(ExternalIdentity.Development, username.Key), username, _clock.GetUtcNow(), 1);
        _accounts.Names[username.Key] = new UsernameReservation(username, id, _clock.GetUtcNow());
        return id;
    }

    [Fact]
    public async Task Asking_by_name_puts_a_request_on_both_lists_and_not_a_friendship()
    {
        var asked = await _friends.AskAsync(_mark, "Bastien", Cancel);

        asked.IsSuccess.ShouldBeTrue();
        asked.Value.ShouldBe(new FriendAsked(_bastien, Accepted: false));
        var mine = await _friends.ListAsync(_mark, Cancel);
        var theirs = await _friends.ListAsync(_bastien, Cancel);
        mine.Outgoing.ShouldBe([new FriendLink(_bastien, FixedTimeProvider.Default)]);
        mine.Friends.ShouldBeEmpty();
        theirs.Incoming.ShouldBe([new FriendLink(_mark, FixedTimeProvider.Default)]);
        (await _friends.AreFriendsAsync(_mark, _bastien, Cancel)).ShouldBeFalse();
    }

    [Fact]
    public async Task Accepting_makes_friends_of_both_and_leaves_no_request()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);

        (await _friends.AcceptAsync(_bastien, _mark, Cancel)).IsSuccess.ShouldBeTrue();

        (await _friends.AreFriendsAsync(_mark, _bastien, Cancel)).ShouldBeTrue();
        (await _friends.AreFriendsAsync(_bastien, _mark, Cancel)).ShouldBeTrue();
        (await _friends.ListAsync(_mark, Cancel)).Outgoing.ShouldBeEmpty();
        (await _friends.ListAsync(_bastien, Cancel)).Incoming.ShouldBeEmpty();
    }

    [Fact]
    public async Task Asking_back_is_accepting()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);

        var asked = await _friends.AskAsync(_bastien, "markf", Cancel);

        asked.Value.Accepted.ShouldBeTrue();
        (await _friends.AreFriendsAsync(_mark, _bastien, Cancel)).ShouldBeTrue();
    }

    [Fact]
    public async Task Nobody_is_asked_twice_a_friend_is_not_asked_and_you_cannot_ask_yourself_or_nobody()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);
        (await _friends.AskAsync(_mark, "bastien", Cancel)).Error.ShouldBe(FriendErrors.AlreadyAsked);

        await _friends.AcceptAsync(_bastien, _mark, Cancel);
        (await _friends.AskAsync(_mark, "bastien", Cancel)).Error.ShouldBe(FriendErrors.AlreadyFriends);
        (await _friends.AskAsync(_mark, "MarkF", Cancel)).Error.ShouldBe(FriendErrors.Self);
        (await _friends.AskAsync(_mark, "nobody-here", Cancel)).Error.ShouldBe(FriendErrors.NoSuchPlayer);
        (await _friends.AskAsync(_mark, "greedy", Cancel)).Error.ShouldBe(FriendErrors.NoSuchPlayer, "a bot's name is nobody's");
        (await _friends.AskAsync(_mark, "", Cancel)).Error.ShouldBe(FriendErrors.NoSuchPlayer);
    }

    [Fact]
    public async Task Accepting_a_request_that_was_never_made_is_refused()
    {
        (await _friends.AcceptAsync(_bastien, _mark, Cancel)).Error.ShouldBe(FriendErrors.NoSuchRequest);
    }

    [Fact]
    public async Task Declining_withdrawing_and_removing_leave_neither_side_with_a_link()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);
        (await _friends.UnlinkAsync(_bastien, _mark, Cancel)).ShouldBeTrue("declined");
        (await _friends.ListAsync(_mark, Cancel)).ShouldBe(FriendList.Empty);
        (await _friends.ListAsync(_bastien, Cancel)).ShouldBe(FriendList.Empty);

        await _friends.AskAsync(_mark, "bastien", Cancel);
        (await _friends.UnlinkAsync(_mark, _bastien, Cancel)).ShouldBeTrue("withdrawn");
        (await _friends.ListAsync(_bastien, Cancel)).Incoming.ShouldBeEmpty();

        await _friends.AskAsync(_mark, "bastien", Cancel);
        await _friends.AcceptAsync(_bastien, _mark, Cancel);
        (await _friends.UnlinkAsync(_mark, _bastien, Cancel)).ShouldBeTrue("removed");
        (await _friends.AreFriendsAsync(_bastien, _mark, Cancel)).ShouldBeFalse();
        (await _friends.UnlinkAsync(_mark, _bastien, Cancel)).ShouldBeFalse("nothing left to remove");
    }

    [Fact]
    public async Task A_declined_player_may_ask_again()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);
        await _friends.UnlinkAsync(_bastien, _mark, Cancel);

        (await _friends.AskAsync(_mark, "bastien", Cancel)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_hundred_and_first_link_is_refused_on_either_side()
    {
        for (var index = 0; index < Friends.MostLinks; index++)
        {
            Register($"player-{index}");
            (await _friends.AskAsync(_mark, $"player-{index}", Cancel)).IsSuccess.ShouldBeTrue();
        }

        (await _friends.AskAsync(_mark, "bastien", Cancel)).Error.ShouldBe(FriendErrors.TooMany, "the asker is full");
        (await _friends.AskAsync(_bastien, "player-7", Cancel)).IsSuccess.ShouldBeTrue("the asked has room: one incoming");
    }

    [Fact]
    public async Task An_account_with_a_hundred_friends_can_still_ask_but_a_hundred_and_first_friendship_is_refused_on_either_side()
    {
        for (var index = 0; index < Friends.MostLinks; index++)
        {
            var friend = Register($"friend-{index}");
            (await _friends.AskAsync(_mark, $"friend-{index}", Cancel)).IsSuccess.ShouldBeTrue();
            (await _friends.AcceptAsync(friend, _mark, Cancel)).IsSuccess.ShouldBeTrue();
        }

        (await _friends.AskAsync(_mark, "bastien", Cancel)).IsSuccess.ShouldBeTrue("a request is not a friendship: the pending limit is its own");
        (await _friends.AcceptAsync(_bastien, _mark, Cancel)).Error.ShouldBe(FriendErrors.TooMany, "Mark has no room for a friend, so Bastien cannot accept");
        (await _friends.AskAsync(_bastien, "markf", Cancel)).Error.ShouldBe(FriendErrors.TooMany, "nor accept by asking back");
        (await _friends.AreFriendsAsync(_mark, _bastien, Cancel)).ShouldBeFalse();
        (await _friends.ListAsync(_bastien, Cancel)).Incoming.ShouldHaveSingleItem("the request stays, for when Mark has room");
    }

    [Fact]
    public async Task Asking_for_or_accepting_an_account_that_is_gone_is_refused()
    {
        await _friends.AskAsync(_bastien, "markf", Cancel);
        _accounts.Accounts.Remove(_bastien);

        (await _friends.AcceptAsync(_mark, _bastien, Cancel)).Error.ShouldBe(FriendErrors.NoSuchPlayer);
        (await _friends.AskAsync(_mark, "bastien", Cancel)).Error.ShouldBe(FriendErrors.NoSuchPlayer, "the name is still held, the account is not");
        (await _friends.AreFriendsAsync(_mark, _bastien, Cancel)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_list_written_on_one_side_only_is_put_right_by_the_next_change_between_the_two()
    {
        await _friends.AskAsync(_mark, "bastien", Cancel);
        await _friends.AcceptAsync(_bastien, _mark, Cancel);
        await _store.SaveAsync(_bastien, FriendList.Empty, Cancel);

        (await _friends.AskAsync(_bastien, "markf", Cancel)).Error.ShouldBe(FriendErrors.AlreadyFriends);

        (await _friends.AreFriendsAsync(_bastien, _mark, Cancel)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_forgotten_account_is_on_nobody_s_list_and_has_none()
    {
        var third = Register("third");
        await _friends.AskAsync(_mark, "bastien", Cancel);
        await _friends.AcceptAsync(_bastien, _mark, Cancel);
        await _friends.AskAsync(_mark, "third", Cancel);
        await _friends.AskAsync(_bastien, "third", Cancel);

        await _friends.ForgetAsync(_mark, Cancel);

        (await _friends.ListAsync(_mark, Cancel)).ShouldBe(FriendList.Empty);
        (await _friends.ListAsync(_bastien, Cancel)).Friends.ShouldBeEmpty();
        (await _friends.ListAsync(third, Cancel)).Incoming.Select(link => link.Other).ShouldBe([_bastien]);
    }

    private sealed class InMemoryFriends : IFriendStore
    {
        private readonly Dictionary<AccountId, FriendList> _held = [];

        public Task<FriendList> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.GetValueOrDefault(account, FriendList.Empty));

        public Task SaveAsync(AccountId account, FriendList list, CancellationToken cancellationToken = default)
        {
            _held[account] = list;
            return Task.CompletedTask;
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
        {
            _held.Remove(account);
            return Task.CompletedTask;
        }
    }
}
