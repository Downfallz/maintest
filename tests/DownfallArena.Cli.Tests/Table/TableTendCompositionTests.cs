using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The hourly job's own composition (#1109): <see cref="TableTend.ComposeAsync" /> builds the door every notice goes
/// through (ADR 0201) without push keys, and hands it to its lobby. The games are played on the API host as the command
/// line composes it, and the job is composed over the same store, as it runs beside the host.
/// </summary>
public sealed class TableTendCompositionTests : IAsyncDisposable
{
    private readonly HostedTables _hosted = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-tend-composed-{Guid.NewGuid():N}");
    private readonly List<IDisposable> _held = [];
    private ComposedHost? _host;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private string Record => Path.Combine(_root, "playtest");

    private static AccountRequest Signed(string session) => new(session, Secure: true);

    public async ValueTask DisposeAsync()
    {
        foreach (var held in Enumerable.Reverse(_held))
        {
            held.Dispose();
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

    /// <summary>The API host as the command line composes it over the store, with push keys or without.</summary>
    private async Task<ComposedHost> HostAsync(bool push = false)
    {
        var options = new CliOptions { Command = "table", Output = "out.csv", Record = Record, Accounts = AccountsMode.Development, Lobby = true, Port = FreePort() };
        _host = (await TableHost.ComposeAsync(_hosted.Services, options, HostedTables.Rules, push ? new TableHost.Pushing(new RecordedPush(), "BP8") : null, Path.Combine(AppContext.BaseDirectory, "table"), Cancel)).ShouldNotBeNull();
        return _host;
    }

    /// <summary>The job as the command line composes it over the host's store, with no push keys.</summary>
    private async Task<TableTend.Tending> JobAsync(RecordedPush? sender = null)
    {
        var options = new CliOptions { Command = CliOptions.TendCommand, Output = "out.csv", Record = Record, Accounts = AccountsMode.Development };
        var job = await TableTend.ComposeAsync(_hosted.Services, options, HostedTables.Rules, sender, Record);
        _held.Add(job);
        return job;
    }

    private static async Task<(string Session, AccountId Id)> SignInAsync(ComposedHost host, string name, bool phone = false)
    {
        var signedIn = await host.Accounts!.HandleAsync("POST", "/api/account/dev", $$"""{"username":"{{name}}"}""", new AccountRequest(null, Secure: true));
        var session = signedIn.Headers!.Single(header => header.Key == "Set-Cookie").Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
        if (phone)
        {
            var subscription = JsonSerializer.Serialize(new { endpoint = $"https://fcm.googleapis.com/fcm/send/{name}", keys = new { p256dh = InMemoryDeviceStore.Key, auth = InMemoryDeviceStore.Auth } });
            (await host.Accounts.HandleAsync("PUT", "/api/account/push", subscription, Signed(session))).Status.ShouldBe(200);
        }

        return (session, (await host.Accounts.WhoAsync(Signed(session), Cancel)).ShouldNotBeNull().Account.Id);
    }

    private static async Task<IReadOnlyList<string?>> KindsAsync(ComposedHost host, string session)
    {
        var answer = await host.Accounts!.HandleAsync("GET", "/api/account/notices", string.Empty, Signed(session));
        return [.. JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("notices").EnumerateArray().Select(entry => entry.GetProperty("kind").GetString())];
    }

    /// <summary>A game between the two accounts with a day to answer, begun on the host: the first has answered, the second is asked.</summary>
    private async Task<PlayedTable> BegunAsync(ComposedHost host, AccountId first, AccountId second)
    {
        var table = await host.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        _held.Add(table);
        table.TurnLimit = TimeSpan.FromHours(24);
        using (var place = (await host.Registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await host.Registry.AddAsync(table, place, Cancel);
        }

        table.Seats[0].Account = first;
        table.Seats[1].Account = second;
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        foreach (var seat in table.Seats)
        {
            for (var attempt = 0; attempt < 300 && seat.Person!.Waiting is null; attempt++)
            {
                await Task.Delay(20, Cancel);
            }
        }

        var asked = table.Seats[0].Person!.Waiting.ShouldNotBeNull().Asked;
        var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
        (await table.Api.HandleAsync("POST", $"/api/seat/{table.Seats[0].Name}/decision", pass, table.Seats[0].Token)).Status.ShouldBe(204);
        await table.RecordAsync(Cancel);
        return table;
    }

    [Fact]
    public async Task Without_push_keys_a_game_the_job_times_out_is_kept_in_each_player_s_bell()
    {
        var host = await HostAsync();
        var mark = await SignInAsync(host, "MarkF");
        var bastien = await SignInAsync(host, "bastien");
        var table = await BegunAsync(host, mark.Id, bastien.Id);
        var job = await JobAsync();

        var tended = await TableTend.TendAsync(job.Registry, job.Lobby, DateTimeOffset.UtcNow + TimeSpan.FromHours(25), job.Composer);

        tended.TimedOut.ShouldBe([table.Id]);
        (await KindsAsync(host, mark.Session)).ShouldBe(["match-over"]);
        (await KindsAsync(host, bastien.Session)).ShouldBe(["match-over"]);
    }

    [Fact]
    public async Task A_suspended_account_s_seat_the_job_offers_to_others_is_news_in_the_other_player_s_bell()
    {
        var host = await HostAsync();
        var mark = await SignInAsync(host, "MarkF");
        var bastien = await SignInAsync(host, "bastien");
        await BegunAsync(host, mark.Id, bastien.Id);
        var at = DateTimeOffset.UtcNow;
        (await host.Accounts!.SuspendAsync(bastien.Id, AccountSuspension.Create("Spamming invitations to strangers.", "op", at).Value, Cancel)).IsSuccess.ShouldBeTrue();
        var job = await JobAsync();

        var tended = await TableTend.TendAsync(job.Registry, job.Lobby, at + AccountSuspension.SeatsKeptFor, suspended: job.Suspended);

        tended.Released.ShouldBe([bastien.Id.Value]);
        (await KindsAsync(host, mark.Session)).ShouldBe(["left"]);
    }

    [Fact]
    public async Task A_notice_the_job_gives_an_account_that_is_gone_is_kept_in_no_bell()
    {
        await HostAsync();
        var job = await JobAsync();
        var gone = AccountId.New();

        await job.Composer.Notifier!.NotifyAsync(gone, new Notice(NoticeKind.MatchOver, "Defeat", "MarkF won.", "/play?table=t1", "match:t1"), Cancel);

        (await job.Notices!.ListAsync(gone, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task With_push_keys_a_result_the_account_turned_off_is_kept_in_its_bell_and_pushed_by_the_job_to_none_of_its_devices()
    {
        var host = await HostAsync(push: true);
        var mark = await SignInAsync(host, "MarkF", phone: true);
        var bastien = await SignInAsync(host, "bastien", phone: true);
        (await host.Accounts!.HandleAsync("PUT", "/api/account/preferences/notices", """{"results":false}""", Signed(mark.Session))).Status.ShouldBe(200);
        await BegunAsync(host, mark.Id, bastien.Id);
        var sender = new RecordedPush();
        var job = await JobAsync(sender);

        await TableTend.TendAsync(job.Registry, job.Lobby, DateTimeOffset.UtcNow + TimeSpan.FromHours(25), job.Composer);

        sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://fcm.googleapis.com/fcm/send/bastien"]);
        (await KindsAsync(host, mark.Session)).ShouldBe(["match-over"]);
    }

    /// <summary>A port nothing else on this machine holds right now, asked of the machine rather than guessed.</summary>
    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
