using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The one door of every notice to an account (ADR 0201): the bell keeps the news whatever the push does, what waits
/// is left to the bell's own lists, and a bell that cannot be written never stops the push nor the request.
/// </summary>
public sealed class AccountNotifierTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly AccountId _mark = AccountId.New();
    private readonly AccountNoticeLog _log = new(new InMemoryNoticeStore(), TimeProvider.System);
    private readonly RecordedPush _sender = new();
    private readonly AccountDevices _devices = new(new InMemoryDeviceStore(), TimeProvider.System);

    private static Notice Over => new(NoticeKind.MatchOver, "Victory", "You won against bastien.", "/play?table=t1", "match:t1");

    private static Notice Invited => new(NoticeKind.Invitation, "An invitation", "bastien invites you to a 3v3 battle.", "/lobby#invitations", "invite:t1");

    private async Task<AccountNotices> PushAsync()
    {
        await _devices.AddAsync(_mark, new PushDevice("https://push.example/phone", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        return new AccountNotices(_sender, _devices);
    }

    [Fact]
    public async Task With_push_off_news_is_kept_in_the_bell()
    {
        var notifier = new AccountNotifier(_log, push: null);

        var reached = await notifier.NotifyAsync(_mark, Over, Cancel);

        reached.ShouldBe(0);
        var kept = (await _log.ListAsync(_mark, Cancel)).ShouldHaveSingleItem();
        kept.ShouldBe(new NoticeEntry(kept.Id, NoticeKind.MatchOver, "Victory", "You won against bastien.", "/play?table=t1", "match:t1", kept.At, Read: false));
    }

    [Fact]
    public async Task With_push_on_news_is_kept_and_pushed_under_its_topic()
    {
        var notifier = new AccountNotifier(_log, await PushAsync());

        var reached = await notifier.NotifyAsync(_mark, Over, Cancel);

        reached.ShouldBe(1);
        _sender.Sent.ShouldHaveSingleItem().Topic.ShouldBe("match");
        (await _log.ListAsync(_mark, Cancel)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task What_waits_is_pushed_and_not_kept_as_news()
    {
        var notifier = new AccountNotifier(_log, await PushAsync());

        await notifier.NotifyAsync(_mark, Invited, Cancel);

        _sender.Sent.ShouldHaveSingleItem().Topic.ShouldBe("invitation");
        (await _log.ListAsync(_mark, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_kept_notice_tells_the_account_s_lobby_to_read_again()
    {
        var changes = new LobbyChanges();
        var notifier = new AccountNotifier(_log, push: null) { Changes = changes };

        await notifier.NotifyAsync(_mark, Over, Cancel);

        changes.VersionOf(_mark).ShouldBe(1);
    }

    [Fact]
    public async Task A_bell_that_cannot_be_written_says_so_and_the_push_still_goes()
    {
        var said = new List<string>();
        var notifier = new AccountNotifier(new AccountNoticeLog(new NoticesOut(), TimeProvider.System), await PushAsync()) { Said = said.Add };

        var reached = await notifier.NotifyAsync(_mark, Over, Cancel);

        reached.ShouldBe(1);
        said.ShouldHaveSingleItem().ShouldContain("could not be kept in its bell");
    }

    [Fact]
    public async Task An_account_that_is_gone_gets_no_bell()
    {
        var notifier = new AccountNotifier(_log, push: null) { Exists = (_, _) => Task.FromResult(false) };

        await notifier.NotifyAsync(_mark, Over, Cancel);

        (await _log.ListAsync(_mark, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_log_written_while_its_account_was_being_deleted_is_forgotten_again()
    {
        var asked = 0;
        var changes = new LobbyChanges();
        var notifier = new AccountNotifier(_log, push: null) { Exists = (_, _) => Task.FromResult(++asked == 1), Changes = changes };

        await notifier.NotifyAsync(_mark, Over, Cancel);

        (await _log.ListAsync(_mark, Cancel)).ShouldBeEmpty();
        changes.VersionOf(_mark).ShouldBe(0);
    }

    [Fact]
    public async Task A_kind_the_account_turned_off_is_kept_in_the_bell_and_not_pushed()
    {
        var notifier = new AccountNotifier(_log, await PushAsync()) { Preferences = Chose(new NoticePreferences(Turns: true, Invitations: true, Results: false)) };

        var reached = await notifier.NotifyAsync(_mark, Over, Cancel);

        reached.ShouldBe(0);
        _sender.Sent.ShouldBeEmpty();
        (await _log.ListAsync(_mark, Cancel)).ShouldHaveSingleItem().Kind.ShouldBe(NoticeKind.MatchOver);
    }

    [Fact]
    public async Task A_kind_the_account_kept_is_pushed_whatever_it_turned_off_elsewhere()
    {
        var notifier = new AccountNotifier(_log, await PushAsync()) { Preferences = Chose(new NoticePreferences(Turns: false, Invitations: true, Results: false)) };

        await notifier.NotifyAsync(_mark, Invited, Cancel);

        _sender.Sent.ShouldHaveSingleItem().Topic.ShouldBe("invitation");
    }

    [Fact]
    public async Task Choices_that_cannot_be_read_leave_the_notice_pushed_and_say_so()
    {
        var said = new List<string>();
        var preferences = Substitute.For<IPreferenceStore>();
        preferences.GetNoticesAsync(_mark, Arg.Any<CancellationToken>()).Returns<NoticePreferences>(_ => throw new IOException("The preferences are out."));
        var notifier = new AccountNotifier(_log, await PushAsync()) { Preferences = preferences, Said = said.Add };

        var reached = await notifier.NotifyAsync(_mark, Over, Cancel);

        reached.ShouldBe(1);
        said.ShouldHaveSingleItem().ShouldContain("what pops up could not be read");
    }

    [Fact]
    public async Task A_device_named_as_an_exception_is_not_pushed()
    {
        var push = await PushAsync();
        await _devices.AddAsync(_mark, new PushDevice("https://push.example/laptop", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        var notifier = new AccountNotifier(_log, push);

        var reached = await notifier.NotifyAsync(_mark, Invited, ["https://push.example/phone"], Cancel);

        reached.ShouldBe(1);
        _sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/laptop");
    }

    private IPreferenceStore Chose(NoticePreferences chosen)
    {
        var preferences = Substitute.For<IPreferenceStore>();
        preferences.GetNoticesAsync(_mark, Arg.Any<CancellationToken>()).Returns(chosen);
        return preferences;
    }

    [Theory]
    [InlineData(NoticeKind.Turn, "turn")]
    [InlineData(NoticeKind.FriendRequest, "friend")]
    [InlineData(NoticeKind.FriendAccepted, "friend")]
    [InlineData(NoticeKind.Invitation, "invitation")]
    [InlineData(NoticeKind.SeatTaken, "invitation")]
    [InlineData(NoticeKind.Declined, "invitation")]
    [InlineData(NoticeKind.Left, "match")]
    [InlineData(NoticeKind.MatchOver, "match")]
    public void Each_kind_is_pushed_under_the_topic_it_was_sent_under_before_the_door(NoticeKind kind, string topic) =>
        AccountNotifier.TopicOf(kind).ShouldBe(topic);

    /// <summary>A notice store that is out, as one whose storage account does not answer.</summary>
    private sealed class NoticesOut : INoticeStore
    {
        public Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default) => throw new IOException("The notices are out.");

        public Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default) => throw new IOException("The notices are out.");

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) => throw new IOException("The notices are out.");
    }
}
