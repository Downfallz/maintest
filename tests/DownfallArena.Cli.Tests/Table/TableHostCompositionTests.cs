using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The <c>table</c> command's own composition (#1109): every other host test builds the door every notice goes through
/// by hand (ADR 0201), so a setter only <see cref="TableHost" /> assigns could be removed with the suite green. Here
/// the host is composed as it runs, with accounts on, and each wire is seen through what a player gets from it.
/// </summary>
public sealed class TableHostCompositionTests : IAsyncDisposable
{
    private const string Phone = "https://push.example/phone";
    private const string Subscription = """{"endpoint":"https://push.example/phone","keys":{"p256dh":"BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A","auth":"AAECAwQFBgcICQoLDA0ODw"}}""";
    private const string Browser = """{"endpoint":"https://push.example/browser","expirationTime":null,"keys":{"p256dh":"BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A","auth":"AAECAwQFBgcICQoLDA0ODw"}}""";

    private readonly HostedTables _hosted = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-composed-{Guid.NewGuid():N}");
    private readonly RecordedPush _sender = new();
    private readonly List<PlayedTable> _tables = [];
    private ComposedHost? _host;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static AccountRequest Signed(string session) => new(session, Secure: true);

    private static TableRequest TwoPeople => new(null, null, Who: null, Handover: 999, Seed: 7);

    public async ValueTask DisposeAsync()
    {
        foreach (var table in _tables)
        {
            table.Dispose();
        }

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _hosted.Dispose();
        for (var attempt = 1; Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(100, CancellationToken.None);
            }
        }
    }

    /// <summary>The host the command line composes over a directory, signing players in, with push keys or without.</summary>
    private async Task<ComposedHost> ComposeAsync(bool push = false, TurnsMode? turns = null, IChangeRelay? relay = null)
    {
        var options = new CliOptions
        {
            Command = "table",
            Output = "out.csv",
            Record = Path.Combine(_root, "playtest"),
            Accounts = AccountsMode.Development,
            Lobby = true,
            Port = FreePort(),
            Turns = turns,
        };
        _host = (await TableHost.ComposeAsync(_hosted.Services, options, HostedTables.Rules, push ? new TableHost.Pushing(_sender, "BP8") : null, Path.Combine(AppContext.BaseDirectory, "table"), relay, Cancel)).ShouldNotBeNull();
        return _host;
    }

    /// <summary>Signs the name in through the host's own sign-in, and answers its session and its account.</summary>
    private static async Task<(string Session, AccountId Id)> SignInAsync(ComposedHost host, string name, bool phone = false)
    {
        var signedIn = await host.Accounts!.HandleAsync("POST", "/api/account/dev", $$"""{"username":"{{name}}"}""", new AccountRequest(null, Secure: true));
        var session = signedIn.Headers!.Single(header => header.Key == "Set-Cookie").Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
        if (phone)
        {
            (await host.Accounts.HandleAsync("PUT", "/api/account/push", Subscription.Replace("/phone", $"/{name}", StringComparison.Ordinal), Signed(session))).Status.ShouldBe(200);
        }

        return (session, (await host.Accounts.WhoAsync(Signed(session), Cancel)).ShouldNotBeNull().Account.Id);
    }

    private static async Task<JsonElement> NoticesAsync(ComposedHost host, string session)
    {
        var answer = await host.Accounts!.HandleAsync("GET", "/api/account/notices", string.Empty, Signed(session));
        answer.Status.ShouldBe(200);
        return Json(answer).GetProperty("notices");
    }

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static Notice News => new(NoticeKind.FriendAccepted, "A new friend", "bastien accepted your friend request.", "/lobby#friends", "friend:test");

    /// <summary>A match between the two accounts, composed by the host's composer and played to its end by the understudies.</summary>
    private async Task<PlayedTable> FinishedBetweenAsync(ComposedHost host, AccountId first, AccountId second)
    {
        var table = await host.Composer.ComposeAsync(TwoPeople, Cancel);
        _tables.Add(table);
        table.Seats[0].Account = first;
        table.Seats[1].Account = second;
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(60), Cancel);
        for (var attempt = 0; attempt < 500 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        return table;
    }

    /// <summary>A table with a person in seat 1 held by the account, in the host's registry, as the lobby holds it.</summary>
    private async Task<PlayedTable> HeldAsync(ComposedHost host, AccountId account)
    {
        var table = await host.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        _tables.Add(table);
        using (var place = (await host.Registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await host.Registry.AddAsync(table, place, Cancel);
        }

        table.Seats[0].Account = account;
        return table;
    }

    [Fact]
    public async Task On_a_host_without_push_keys_a_friend_accepting_is_listed_in_the_asker_s_notices()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        var bastien = await SignInAsync(host, "bastien");
        (await host.Friends!.HandleAsync("POST", "/api/friends/requests", """{"username":"bastien"}""", Signed(mark.Session))).Status.ShouldBe(200);

        var accepted = await host.Friends.HandleAsync("POST", $"/api/friends/requests/{mark.Id.Value}/accept", string.Empty, Signed(bastien.Session));

        accepted.Status.ShouldBe(200);
        var kept = (await NoticesAsync(host, mark.Session)).EnumerateArray().ShouldHaveSingleItem();
        kept.GetProperty("kind").GetString().ShouldBe("friend-accepted");
        kept.GetProperty("body").GetString().ShouldBe("bastien accepted your friend request.");
    }

    [Fact]
    public async Task A_notice_the_bell_keeps_tells_the_account_s_pages()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        var before = host.Changes.VersionOf(mark.Id);

        await host.Notifier!.NotifyAsync(mark.Id, News, Cancel);

        host.Changes.VersionOf(mark.Id).ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task A_notice_to_an_account_that_is_gone_is_kept_in_no_bell()
    {
        var host = await ComposeAsync();
        var gone = AccountId.New();

        await host.Notifier!.NotifyAsync(gone, News, Cancel);

        (await host.Notifier.Log!.ListAsync(gone, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Marking_the_notices_read_tells_the_account_s_other_pages()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        await host.Notifier!.NotifyAsync(mark.Id, News, Cancel);
        var upTo = (await NoticesAsync(host, mark.Session))[0].GetProperty("id").GetInt64();
        var before = host.Changes.VersionOf(mark.Id);

        var read = await host.Accounts!.HandleAsync("POST", "/api/account/notices/read", $$"""{"upTo":{{upTo}}}""", Signed(mark.Session));

        read.Status.ShouldBe(200);
        host.Changes.VersionOf(mark.Id).ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task A_match_the_host_ends_is_kept_in_each_player_s_bell()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        var bastien = await SignInAsync(host, "bastien");

        var table = await FinishedBetweenAsync(host, mark.Id, bastien.Id);

        foreach (var session in new[] { mark.Session, bastien.Session })
        {
            var kept = (await NoticesAsync(host, session)).EnumerateArray().ShouldHaveSingleItem();
            kept.GetProperty("kind").GetString().ShouldBe("match-over");
            kept.GetProperty("url").GetString().ShouldBe($"/play?table={table.Id}");
        }
    }

    [Fact]
    public async Task A_result_an_account_turned_off_is_kept_in_its_bell_and_pushed_to_none_of_its_devices()
    {
        var host = await ComposeAsync(push: true);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        var bastien = await SignInAsync(host, "bastien", phone: true);
        (await host.Accounts!.HandleAsync("PUT", "/api/account/preferences/notices", """{"results":false}""", Signed(mark.Session))).Status.ShouldBe(200);

        await FinishedBetweenAsync(host, mark.Id, bastien.Id);

        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/bastien"]);
        (await NoticesAsync(host, mark.Session)).EnumerateArray().ShouldHaveSingleItem().GetProperty("kind").GetString().ShouldBe("match-over");
    }

    [Fact]
    public async Task A_signed_in_seat_s_subscription_joins_its_account_s_devices()
    {
        var host = await ComposeAsync(push: true);
        var mark = await SignInAsync(host, "MarkF");
        var table = await HeldAsync(host, mark.Id);
        var player = (await host.Accounts!.WhoAsync(Signed(mark.Session), Cancel)).ShouldNotBeNull();

        var subscribed = await host.Admin.Lobby.HandleAsync("PUT", "/api/lobby/push", Browser, table.Seats[0].Token, player);

        subscribed.Status.ShouldBe(200);
        (await host.Accounts.Devices!.ListAsync(mark.Id, Cancel)).Select(device => device.Endpoint).ShouldBe(["https://push.example/browser"]);
    }

    [Fact]
    public async Task A_signed_in_seat_s_turn_reaches_a_device_of_its_account()
    {
        var host = await ComposeAsync(push: true);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        var table = await HeldAsync(host, mark.Id);
        table.Seats[0].Person!.Arrive();
        table.Session.Begin();
        for (var attempt = 0; attempt < 300 && table.Seats[0].Person!.Waiting is null; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        var asked = table.Seats[0].Person!.Waiting.ShouldNotBeNull().Asked;

        await host.Push!.TurnDueAsync(table, new DueTurn(table.Id, table.Seats[0].Name, asked, 1));

        _sender.Sent.Select(sent => sent.Endpoint).ShouldContain("https://push.example/MarkF");
    }

    [Fact]
    public async Task On_a_host_with_a_notices_queue_a_notice_goes_on_it_and_the_notices_worker_sends_it()
    {
        var host = await ComposeAsync(push: true, turns: TurnsMode.Queue);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        await host.Notifier!.NotifyAsync(mark.Id, News, Cancel);
        var beforeTheWorker = _sender.Sent.Count;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var noticing = await TableHost.StartNoticesAsync(host.Channels, host.Notices, host.Push, host.Registry, TimeProvider.System, stopping.Token);
        for (var attempt = 0; attempt < 300 && _sender.Sent.Count == 0; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        await stopping.CancelAsync();
        await noticing;
        beforeTheWorker.ShouldBe(0, "the door queued the notice rather than send it in the request");
        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/MarkF"]);
    }

    [Fact]
    public async Task A_friend_request_moves_the_asker_s_own_lists_on_their_other_pages()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        await SignInAsync(host, "bastien");
        var before = host.Changes.VersionOf(mark.Id);

        (await host.Friends!.HandleAsync("POST", "/api/friends/requests", """{"username":"bastien"}""", Signed(mark.Session))).Status.ShouldBe(200);

        host.Changes.VersionOf(mark.Id).ShouldBeGreaterThan(before, "the request is told to the asker's pages, which no notice is");
    }

    [Fact]
    public async Task Started_as_it_runs_the_host_sends_what_its_notices_queue_holds()
    {
        var host = await ComposeAsync(push: true, turns: TurnsMode.Queue);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        await host.Notifier!.NotifyAsync(mark.Id, News, Cancel);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var running = await TableHost.StartAsync(host, TimeProvider.System, stopping.Token);
        for (var attempt = 0; attempt < 600 && _sender.Count == 0; attempt++)
        {
            await Task.Delay(50, Cancel);
        }

        await stopping.CancelAsync();
        await running;
        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/MarkF"]);
    }

    [Fact]
    public async Task Started_as_it_runs_on_a_queue_the_host_takes_the_bots_turns_it_puts_there()
    {
        var host = await ComposeAsync(turns: TurnsMode.Queue);
        var table = await host.Composer.ComposeAsync(TwoPeople, Cancel);
        _tables.Add(table);
        using (var place = (await host.Registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await host.Registry.AddAsync(table, place, Cancel);
        }

        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        await table.RecordAsync(Cancel);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var running = await TableHost.StartAsync(host, TimeProvider.System, stopping.Token);
        await Task.WhenAny(table.Session.Outcome, Task.Delay(TimeSpan.FromSeconds(60), Cancel));

        await stopping.CancelAsync();
        await running;
        table.Session.Outcome.IsCompleted.ShouldBeTrue("the understudies' turns went on the queue and the host's worker took them");
    }

    [Fact]
    public async Task With_a_relay_a_notice_the_host_s_door_keeps_is_said_to_the_account_s_pages_through_it()
    {
        var relay = new RecordedRelay();
        var host = await ComposeAsync(relay: relay);
        var mark = await SignInAsync(host, "MarkF");
        relay.Said.Clear();

        await host.Notifier!.NotifyAsync(mark.Id, News, Cancel);

        relay.Said.ShouldBe([$"account {mark.Id.Value}"]);
    }

    [Fact]
    public async Task A_pass_run_from_the_panel_tends_with_every_part_the_host_composed()
    {
        var host = await ComposeAsync();

        var chores = host.Chores.ShouldNotBeNull();

        (chores.Registry, chores.Lobby, chores.Composer).ShouldBe((host.Registry, host.Admin.Lobby, host.Composer));
        chores.Suspended.ShouldNotBeNull();
        chores.Feedback.ShouldNotBeNull();
        chores.FeedbackIndex.ShouldNotBeNull();
        chores.Notices.ShouldNotBeNull().ShouldBeSameAs(host.Accounts!.NoticeLog);
    }

    [Fact]
    public async Task A_table_opened_for_a_friend_tells_the_friend_s_devices()
    {
        var host = await ComposeAsync(push: true);
        var mark = await SignInAsync(host, "MarkF");
        var bastien = await SignInAsync(host, "bastien", phone: true);
        await host.Friends!.HandleAsync("POST", "/api/friends/requests", """{"username":"bastien"}""", Signed(mark.Session));
        await host.Friends.HandleAsync("POST", $"/api/friends/requests/{mark.Id.Value}/accept", string.Empty, Signed(bastien.Session));
        var inviter = (await host.Accounts!.WhoAsync(Signed(mark.Session), Cancel)).ShouldNotBeNull();

        var opened = await host.Admin.Lobby.HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"friend","friend":"{{bastien.Id.Value}}"}""", player: inviter);

        opened.Status.ShouldBe(201);
        var told = _sender.Sent.Last(sent => sent.Endpoint == "https://push.example/bastien");
        told.Topic.ShouldBe("invitation");
    }

    [Fact]
    public async Task On_a_host_with_a_notices_queue_a_signed_in_seat_s_turn_goes_on_it()
    {
        var host = await ComposeAsync(push: true, turns: TurnsMode.Queue);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        var table = await HeldAsync(host, mark.Id);

        table.Seats[0].Person!.Arrive();
        table.Session.Begin();

        var queued = 0;
        for (var attempt = 0; attempt < 250 && queued == 0 && _sender.Sent.Count == 0; attempt++)
        {
            await Task.Delay(20, Cancel);
            queued = await host.Notices!.CountAsync(Cancel);
        }

        queued.ShouldBe(1);
        (await host.Notices!.PeekAsync(1, Cancel)).ShouldBeEmpty("the turn itself is queued, hidden until its grace is over, not a push to a device");
        _sender.Sent.ShouldBeEmpty("the turn is the notices worker's to send, off the request");
    }

    /// <summary>
    /// The push service said an endpoint is gone to another process -- the worker, another replica (#1134): this host's
    /// own instances read it from the device store they share, and refuse the subscription 410 on both routes.
    /// </summary>
    [Fact]
    public async Task An_endpoint_another_process_heard_is_gone_is_refused_by_the_account_and_the_seat_routes()
    {
        var host = await ComposeAsync(push: true);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        var table = await HeldAsync(host, mark.Id);
        var player = (await host.Accounts!.WhoAsync(Signed(mark.Session), Cancel)).ShouldNotBeNull();
        var elsewhere = new RecordedPush { Gone = { "https://push.example/MarkF" } };
        var devices = new AccountDevices(await ArtifactStores.OpenDevicesAsync(Path.Combine(_root, "playtest"), Cancel), TimeProvider.System);
        await new AccountNotices(elsewhere, devices).SendAsync(mark.Id, AccountNotices.Payload("Victory", "You won.", "/lobby", "match:t1"), "match", except: null, Cancel);
        var subscription = Subscription.Replace("/phone", "/MarkF", StringComparison.Ordinal);

        var account = await host.Accounts.HandleAsync("PUT", "/api/account/push", subscription, Signed(mark.Session));
        var seat = await host.Admin.Lobby.HandleAsync("PUT", "/api/lobby/push", subscription, table.Seats[0].Token, player);

        account.Status.ShouldBe(410);
        Json(account).GetProperty("error").GetString().ShouldBe("Account.DeviceGone");
        seat.Status.ShouldBe(410);
    }

    /// <summary>The panel's "Run now" runs the hourly job's pass (A3), the notice logs' sweep with it (#1118).</summary>
    [Fact]
    public async Task A_pass_run_from_the_panel_erases_the_notices_past_90_days()
    {
        var host = await ComposeAsync();
        var mark = await SignInAsync(host, "MarkF");
        var store = await ArtifactStores.OpenNoticesAsync(Path.Combine(_root, "playtest"), Cancel);
        await store.WriteAsync(mark.Id, [new NoticeEntry(1, NoticeKind.MatchOver, "Victory", "You won against bastien.", "/play?table=t1", "match:t1", DateTimeOffset.UtcNow.AddDays(-91), Read: true)], null, Cancel);

        var pass = await host.Admin.Jobs!.HandleAsync("POST", JobsApi.TendRoute, null, "op");

        pass.Status.ShouldBe(200);
        (await store.ReadAsync(mark.Id, Cancel)).Version.ShouldBeNull();
    }

    /// <summary>A port nothing else on this machine holds right now, asked of the machine rather than guessed.</summary>
    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
