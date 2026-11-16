using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The relay (ADR 0165): what a process says through it when a table or a lobby moves, where it sends a page to hear
/// it, and what a process does with what the others said.
/// </summary>
public sealed class RelayTests : IDisposable
{
    private static readonly AccountId Mark = AccountId.Parse("acct_markmarkmarkmark");

    private readonly HostedTables _hosted = new(recording: false);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose() => _hosted.Dispose();

    [Fact]
    public async Task A_table_that_moves_is_said_through_the_relay_under_its_id()
    {
        var relay = new RecordedRelay();
        _hosted.Composer.Relay = relay;
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);

        table.Api.Changes.Bump();

        relay.Said.ShouldContain($"table {table.Id}");
    }

    [Fact]
    public async Task A_seat_is_sent_to_its_table_s_group_on_a_host_with_a_relay()
    {
        _hosted.Composer.Relay = new RecordedRelay();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        var seat = table.Seats[0];

        var answer = await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}/negotiate", string.Empty, seat.Token);

        answer.Status.ShouldBe(200);
        Url(answer).ShouldBe($"wss://relay.example/client/hubs/downfall?group=table-{table.Id}");
    }

    [Fact]
    public async Task A_seat_on_a_host_without_a_relay_is_told_to_read_the_host_s_own_stream()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        var seat = table.Seats[0];

        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}/negotiate", string.Empty, seat.Token)).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_seat_s_address_is_refused_to_another_seat_s_token()
    {
        _hosted.Composer.Relay = new RecordedRelay();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);

        (await table.Api.HandleAsync("GET", "/api/seat/player1/negotiate", string.Empty, table.Seats[1].Token)).Status.ShouldBe(403);
    }

    [Fact]
    public async Task A_lobby_is_sent_to_the_lobby_as_its_signed_in_player_and_to_nothing_without_a_relay()
    {
        using var registry = _hosted.Registry();
        var lobby = new LobbyApi(registry, _hosted.Composer, TimeProvider.System);
        var account = new DownfallArena.Application.Accounts.Account(Mark, new DownfallArena.Application.Accounts.ExternalIdentity(DownfallArena.Application.Accounts.ExternalIdentity.Development, "markf"), DownfallArena.Application.Accounts.Username.Create("MarkF").Value, default, 0);

        var without = await lobby.HandleAsync("GET", "/api/lobby/negotiate", string.Empty);
        lobby.Relay = new RecordedRelay();
        var signedIn = await lobby.HandleAsync("GET", "/api/lobby/negotiate", string.Empty, player: new SignedIn(account, null));
        var nobody = await lobby.HandleAsync("GET", "/api/lobby/negotiate", string.Empty);

        without.Status.ShouldBe(404);
        Url(signedIn).ShouldEndWith($"user={Mark.Value}");
        Url(nobody).ShouldEndWith("user=nobody");
    }

    [Fact]
    public void The_lobby_s_changes_are_said_through_the_relay_too()
    {
        var relay = new RecordedRelay();
        var changes = new LobbyChanges { Relay = relay };

        changes.Everyone();
        changes.Account(Mark);

        relay.Said.ShouldBe(["lobby", $"account {Mark.Value}"]);
    }

    [Fact]
    public async Task A_burst_of_moves_of_one_table_is_sent_as_two_messages_and_another_table_s_on_its_own()
    {
        var release = new TaskCompletionSource();
        var sent = new List<string>();
        var sends = new CoalescedSends(async key =>
        {
            lock (sent)
            {
                sent.Add(key);
            }

            await release.Task;
        });

        for (var i = 0; i < 5; i++)
        {
            sends.Post("t:one");
        }

        sends.Post("t:two");
        await Task.Delay(50, Cancel);
        release.SetResult();
        await sends.Idle;

        sent.Count(key => key == "t:one").ShouldBe(2, "the first, and one for the four that came while it was on its way");
        sent.Count(key => key == "t:two").ShouldBe(1);
    }

    [Fact]
    public async Task A_send_that_fails_is_given_up_and_the_next_move_sends_again()
    {
        var attempts = 0;
        var sends = new CoalescedSends(_ => ++attempts == 1 ? throw new HttpRequestException("out") : Task.CompletedTask);

        sends.Post("l:");
        await sends.Idle;
        sends.Post("l:");
        await sends.Idle;

        attempts.ShouldBe(2);
    }

    [Theory]
    [InlineData("origin|20261111-1|x", "origin", "20261111-1|x")]
    [InlineData("a|b", "a", "b")]
    public void A_hosts_message_names_who_sent_it_and_the_table(string message, string origin, string table) =>
        WebPubSubRelay.Moved(message).ShouldBe((origin, table));

    [Theory]
    [InlineData("")]
    [InlineData("|table")]
    [InlineData("origin|")]
    [InlineData("changed")]
    public void Anything_else_on_the_hosts_group_names_nothing(string message) =>
        WebPubSubRelay.Moved(message).ShouldBeNull();

    [Fact]
    public async Task A_process_looks_at_everything_as_its_connection_opens_and_at_the_tables_others_moved_but_not_its_own()
    {
        var relay = new RecordedRelay { Origin = "me" };
        var opened = 0;
        var moved = new List<string>();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var listener = new RelayListener(relay, _ => { opened++; return Task.CompletedTask; }, (table, _) => { moved.Add(table); return Task.CompletedTask; }, TimeProvider.System)
        {
            Receive = (_, token) => Messages(["", "worker|one", "me|two", "job|three"], stopping, token),
        };

        await listener.RunAsync(stopping.Token);

        opened.ShouldBe(1);
        moved.ShouldBe(["one", "three"]);
    }

    [Fact]
    public async Task A_dropped_connection_is_opened_again_and_its_opening_looks_at_everything_again()
    {
        var relay = new RecordedRelay();
        var opened = 0;
        var connections = 0;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var listener = new RelayListener(relay, _ => { opened++; return Task.CompletedTask; }, (_, _) => Task.CompletedTask, TimeProvider.System)
        {
            Receive = (_, token) => ++connections < 2 ? throw new System.Net.WebSockets.WebSocketException("dropped") : Messages([""], stopping, token),
        };

        await listener.RunAsync(stopping.Token);

        connections.ShouldBe(2);
        opened.ShouldBe(1, "the first connection dropped before it opened");
    }

    [Fact]
    public async Task A_look_that_could_not_be_made_drops_the_connection_and_the_next_one_looks_at_everything_again()
    {
        var relay = new RecordedRelay();
        var opened = 0;
        var connections = 0;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var listener = new RelayListener(relay, _ => { opened++; return Task.CompletedTask; }, (_, _) => throw new IOException("the store is out"), TimeProvider.System)
        {
            Receive = (_, token) => ++connections == 1 ? Messages(["", "worker|one", "worker|two"], null, token) : Messages([""], stopping, token),
        };

        await listener.RunAsync(stopping.Token);

        connections.ShouldBe(2, "the move that could not be looked at is not dropped: the connection is");
        opened.ShouldBe(2, "and the next opening looks at every table held, that one included");
    }

    [Fact]
    public async Task A_table_s_record_written_is_said_through_the_relay_once_the_write_has_landed()
    {
        using var hosted = new HostedTables(recording: true);
        var relay = new RecordedRelay();
        hosted.Composer.Relay = relay;
        var table = await hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        var before = relay.Said.Count(said => said == $"table {table.Id}");

        await table.RecordAsync(Cancel);

        relay.Said.Count(said => said == $"table {table.Id}").ShouldBeGreaterThan(before);
    }

    [Fact]
    public void The_tables_the_hourly_sweep_closed_are_said_through_the_relay_with_the_lobby()
    {
        var relay = new RecordedRelay();

        TableTend.Announce(relay, new TableTend.Tended(["one", "two"], 0));

        relay.Said.ShouldBe(["table one", "table two", "lobby"]);
    }

    [Fact]
    public void A_sweep_that_closed_nothing_says_nothing()
    {
        var relay = new RecordedRelay();

        TableTend.Announce(relay, new TableTend.Tended([], 3));

        relay.Said.ShouldBeEmpty("an invitation let go of is said by its table's own change");
    }

    // The messages, then the listener is stopped, as a host stopping does.
    // A connection that says these and then closes; with `stopping`, the listener is stopped after them.
    private static async IAsyncEnumerable<string> Messages(string[] messages, CancellationTokenSource? stopping, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await Task.Yield();
            yield return message;
        }

        if (stopping is not null)
        {
            await stopping.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static string Url(DownfallArena.Cli.Studio.StudioResponse response) =>
        JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.GetProperty("url").GetString()!;
}
