using System.Text.Json;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// "Your turn" reaches a phone whose page did not draw the question (ADR 0117), and nothing else: a page that
/// drew it, a seat that was answered, a seat without a subscription, each get no notice.
/// </summary>
public sealed class PushNoticesTests : IDisposable
{
    private static readonly PushSubscription Phone = new("https://push.example/one", "BP8", "YWJj");

    private readonly Sender _sender = new();
    private readonly TableRegistry _registry = new();
    private HostedTables? _hosted;

    public void Dispose()
    {
        _registry.Dispose();
        _hosted?.Dispose();
    }

    [Fact]
    public async Task A_question_nobody_drew_within_the_grace_is_sent_to_the_seat_s_phone_once()
    {
        var (table, notices) = await Seated(grace: TimeSpan.FromMilliseconds(100));
        notices.Record = _ => Task.CompletedTask;
        table.Seats[0].Push = Phone;

        await Arrive(table);

        var (to, payload) = await _sender.Next();
        to.ShouldBe(Phone);
        var notice = JsonDocument.Parse(payload).RootElement;
        notice.GetProperty("title").GetString().ShouldBe("Your turn");
        notice.GetProperty("body").GetString().ShouldBe("Round 1: pick your packages.");
        notice.GetProperty("url").GetString().ShouldBe("/play");
        notice.GetProperty("tag").GetString().ShouldBe("turn:player1");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        _sender.Sent.Count.ShouldBe(1, "one asking, one notice");
    }

    [Fact]
    public async Task A_question_the_page_drew_in_time_is_not_sent()
    {
        var (table, notices) = await Seated(grace: TimeSpan.FromMilliseconds(400));
        table.Seats[0].Push = Phone;

        await Arrive(table);
        var asked = await Asked(table);
        (await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token, null, $"?shown={asked}")).Status.ShouldBe(200);

        await Task.Delay(800, TestContext.Current.CancellationToken);
        _sender.Sent.ShouldBeEmpty();
        notices.PublicKey.ShouldBe("BP8");
    }

    [Fact]
    public async Task A_seat_without_a_subscription_and_a_seat_answered_before_the_grace_get_nothing()
    {
        var (table, _) = await Seated(grace: TimeSpan.FromMilliseconds(300));

        await Arrive(table);
        await Asked(table);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        _sender.Sent.ShouldBeEmpty("no subscription");

        table.Seats[0].Push = Phone;
        var asked = await Asked(table);
        var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
        (await table.Api.HandleAsync("POST", "/api/seat/player1/decision", pass, table.Seats[0].Token)).Status.ShouldBe(204);

        // The next asking, a second Evolution pick or the round's speeds, is the one a notice may go for; the
        // answered one must not be sent after the fact.
        var (_, payload) = await _sender.Next();
        JsonDocument.Parse(payload).RootElement.GetProperty("asked").GetInt64().ShouldBeGreaterThan(asked);
    }

    [Fact]
    public async Task A_subscription_the_service_says_is_gone_is_forgotten_and_the_table_recorded()
    {
        var (table, notices) = await Seated(grace: TimeSpan.Zero);
        var recorded = new TaskCompletionSource<PlayedTable>(TaskCreationOptions.RunContinuationsAsynchronously);
        notices.Record = recordedTable => { recorded.TrySetResult(recordedTable); return Task.CompletedTask; };
        _sender.Gone = true;
        table.Seats[0].Push = Phone;

        await Arrive(table);

        (await recorded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeSameAs(table);
        table.Seats[0].Push.ShouldBeNull();
    }

    [Fact]
    public void The_payload_names_what_is_asked_and_the_round_when_the_question_has_one()
    {
        var seat = new TableSeat(Domain.Matches.PlayerSlot.Player2, "token", new HumanSeat(CancellationToken.None));

        var withRound = JsonDocument.Parse(PushNotices.Payload(new HumanSeat.Question(Application.Matches.Projections.PlayerOptionsKind.Speed, null, 4), 7, seat)).RootElement;
        var atTheSlot = JsonDocument.Parse(PushNotices.Payload(new HumanSeat.Question(Application.Matches.Projections.PlayerOptionsKind.Action, null, 5), null, seat)).RootElement;

        withRound.GetProperty("body").GetString().ShouldBe("Round 7: choose a speed.");
        withRound.GetProperty("asked").GetInt64().ShouldBe(4);
        atTheSlot.GetProperty("body").GetString().ShouldBe("Your creature acts: choose a spell.");
        atTheSlot.GetProperty("tag").GetString().ShouldBe("turn:player2");
    }

    private async Task<(PlayedTable Table, PushNotices Notices)> Seated(TimeSpan grace)
    {
        var notices = new PushNotices(_sender, "BP8", TimeProvider.System, grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        return (table, notices);
    }

    /// <summary>The person reaches their seat, which is what begins the match and asks the first question (ADR 0092).</summary>
    private static async Task Arrive(PlayedTable table) =>
        (await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token)).Status.ShouldBe(200);

    private static async Task<long> Asked(PlayedTable table)
    {
        var person = table.Seats[0].Person!;
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return person.Waiting.ShouldNotBeNull().Asked;
    }

    private sealed class Sender : IPushSender
    {
        private readonly Queue<TaskCompletionSource<(PushSubscription, string)>> _waiting = new();

        public List<(PushSubscription To, string Payload)> Sent { get; } = [];

        public bool Gone { get; set; }

        public Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add((to, payload));
                if (_waiting.TryDequeue(out var waiting))
                {
                    waiting.TrySetResult((to, payload));
                }
            }

            return Task.FromResult(!Gone);
        }

        public Task<(PushSubscription To, string Payload)> Next()
        {
            lock (Sent)
            {
                var source = new TaskCompletionSource<(PushSubscription, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Enqueue(source);
                return source.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }
}
