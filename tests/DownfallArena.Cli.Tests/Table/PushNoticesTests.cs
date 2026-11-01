using System.Text.Json;
using DownfallArena.Cli.Table;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// "Your turn" reaches a phone whose page did not draw the question (ADR 0118), and nothing else: a page that
/// drew it, a seat that was answered, a seat without a subscription, each get no notice. The grace is a clock
/// the test moves, so what is asserted is the rule and not a wait.
/// </summary>
public sealed class PushNoticesTests : IDisposable
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    private static readonly PushSubscription Phone = new(
        "https://push.example/one",
        "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A",
        "AAECAwQFBgcICQoLDA0ODw");

    private readonly Sender _sender = new();
    private readonly FakeTimeProvider _clock = new();
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
        var (table, notices) = await Seated();
        notices.Record = _ => Task.CompletedTask;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        var (to, payload) = await _sender.Next();
        to.ShouldBe(Phone);
        var notice = JsonDocument.Parse(payload).RootElement;
        notice.GetProperty("title").GetString().ShouldBe("Your turn");
        notice.GetProperty("body").GetString().ShouldBe("Round 1: pick your packages.");
        notice.GetProperty("url").GetString().ShouldBe($"/play?table={table.Id}");
        notice.GetProperty("tag").GetString().ShouldBe("turn:player1");
        _clock.Advance(Grace);
        await Settled();
        _sender.Sent.Count.ShouldBe(1, "one asking, one notice");
    }

    [Fact]
    public async Task A_question_the_page_drew_in_time_is_not_sent()
    {
        var (table, notices) = await Seated();
        table.Seats[0].Push = Phone;

        await Arrive(table);
        var asked = await Asked(table);
        (await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token, null, $"?shown={asked}")).Status.ShouldBe(200);
        _clock.Advance(Grace);

        await Settled();
        _sender.Sent.ShouldBeEmpty();
        notices.PublicKey.ShouldBe("BP8");
    }

    [Fact]
    public async Task A_seat_without_a_subscription_and_a_question_answered_before_the_grace_get_nothing()
    {
        var (table, _) = await Seated();

        await Arrive(table);
        var first = await Asked(table);
        _clock.Advance(Grace);
        await Settled();
        _sender.Sent.ShouldBeEmpty("no subscription");

        table.Seats[0].Push = Phone;
        var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked = first });
        (await table.Api.HandleAsync("POST", "/api/seat/player1/decision", pass, table.Seats[0].Token)).Status.ShouldBe(204);
        var next = await Asked(table, after: first);
        _clock.Advance(Grace);

        // The answered asking is not sent after the fact; the one the seat is on now is.
        var (_, payload) = await _sender.Next();
        JsonDocument.Parse(payload).RootElement.GetProperty("asked").GetInt64().ShouldBe(next);
        _sender.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_subscription_the_service_says_is_gone_is_forgotten_recorded_and_refused_from_then_on()
    {
        var (table, notices) = await Seated();
        var recorded = new TaskCompletionSource<PlayedTable>(TaskCreationOptions.RunContinuationsAsynchronously);
        notices.Record = recordedTable => { recorded.TrySetResult(recordedTable); return Task.CompletedTask; };
        _sender.Gone = true;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        (await recorded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeSameAs(table);
        table.Seats[0].Push.ShouldBeNull();
        notices.IsDead(Phone.Endpoint).ShouldBeTrue();
        notices.IsDead("https://push.example/other").ShouldBeFalse();
    }

    /// <summary>A subscription replaced while its notice was in flight is not the one the service refused: the new one stays.</summary>
    [Fact]
    public async Task A_refusal_of_a_subscription_the_seat_no_longer_holds_leaves_the_new_one_alone()
    {
        var (table, notices) = await Seated();
        var recorded = 0;
        notices.Record = _ => { recorded++; return Task.CompletedTask; };
        var replacement = Phone with { Endpoint = "https://push.example/two" };
        _sender.Gone = true;
        _sender.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);
        await _sender.Next();
        table.Seats[0].Push = replacement;
        _sender.Hold.SetResult();
        await Settled();

        table.Seats[0].Push.ShouldBe(replacement);
        recorded.ShouldBe(0);
        notices.IsDead(Phone.Endpoint).ShouldBeTrue();
    }

    /// <summary>A sender that throws is the end of a task nothing awaits: said on the console, and the subscription kept for the next asking.</summary>
    [Fact]
    public async Task A_sender_that_fails_loses_the_notice_and_keeps_the_subscription()
    {
        var (table, _) = await Seated();
        _sender.Failing = true;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        await _sender.Next();
        await Settled();
        table.Seats[0].Push.ShouldBe(Phone);
    }

    [Fact]
    public void The_payload_names_what_is_asked_and_the_round_when_the_question_has_one()
    {
        var seat = new TableSeat(Domain.Matches.PlayerSlot.Player2, "token", new HumanSeat(CancellationToken.None));

        var withRound = JsonDocument.Parse(PushNotices.Payload(new HumanSeat.Question(Application.Matches.Projections.PlayerOptionsKind.Speed, null, 4), 7, seat, "a b")).RootElement;
        var atTheSlot = JsonDocument.Parse(PushNotices.Payload(new HumanSeat.Question(Application.Matches.Projections.PlayerOptionsKind.Action, null, 5), null, seat, "t")).RootElement;

        withRound.GetProperty("url").GetString().ShouldBe("/play?table=a%20b");
        withRound.GetProperty("body").GetString().ShouldBe("Round 7: choose a speed.");
        withRound.GetProperty("asked").GetInt64().ShouldBe(4);
        atTheSlot.GetProperty("body").GetString().ShouldBe("Your creature acts: choose a spell.");
        atTheSlot.GetProperty("tag").GetString().ShouldBe("turn:player2");
    }

    [Fact]
    public async Task One_phone_holding_both_seats_is_hotseat_and_is_not_notified()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        table.Seats[0].Push = Phone;
        table.Seats[1].Push = Phone;

        await Arrive(table);
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", table.Seats[1].Token)).Status.ShouldBe(200);
        var first = await Asked(table);
        _clock.Advance(Grace);
        await Settled();
        _sender.Sent.ShouldBeEmpty("both seats are on one phone, and the person holding it is the other player");

        // The second seat reaches another phone: both are away from their pages, and both are told.
        table.Seats[1].Push = Phone with { Endpoint = "https://push.example/two" };
        var pass = JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked = first });
        (await table.Api.HandleAsync("POST", "/api/seat/player1/decision", pass, table.Seats[0].Token)).Status.ShouldBe(204);
        var other = table.Seats[1].Person!.Waiting.ShouldNotBeNull();
        (await table.Api.HandleAsync("POST", "/api/seat/player2/decision", JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked = other.Asked }), table.Seats[1].Token)).Status.ShouldBe(204);
        await Asked(table, after: first);
        _clock.Advance(Grace);

        var (to, _) = await _sender.Next();
        var (second, _) = await _sender.Next();
        new[] { to.Endpoint, second.Endpoint }.ShouldBe(["https://push.example/one", "https://push.example/two"], ignoreOrder: true);
    }

    private async Task<(PlayedTable Table, PushNotices Notices)> Seated()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        return (table, notices);
    }

    /// <summary>The person reaches their seat, which is what begins the match and asks the first question (ADR 0092).</summary>
    private static async Task Arrive(PlayedTable table) =>
        (await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token)).Status.ShouldBe(200);

    /// <summary>The asking the seat is on, once it is on one past <paramref name="after" />.</summary>
    private static async Task<long> Asked(PlayedTable table, long after = 0)
    {
        var person = table.Seats[0].Person!;
        for (var attempt = 0; attempt < 300 && (person.Waiting?.Asked ?? 0) <= after; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        // The asking is raised right after it is set; a moment for the notice task to have started its grace,
        // so that moving the clock finds a timer to fire.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        return person.Waiting.ShouldNotBeNull().Asked;
    }

    /// <summary>Long enough for a notice task released by the clock to have run to its end on the thread pool.</summary>
    private static Task Settled() => Task.Delay(150, TestContext.Current.CancellationToken);

    /// <summary>What was sent, read in order: a send released by the clock runs to its end inside <c>Advance</c>, before the test asks for it.</summary>
    private sealed class Sender : IPushSender
    {
        private readonly Queue<TaskCompletionSource<(PushSubscription, string)>> _waiting = new();
        private int _read;

        public List<(PushSubscription To, string Payload)> Sent { get; } = [];

        public bool Gone { get; set; }

        public bool Failing { get; set; }

        /// <summary>Set, the send waits on it before answering: what a test needs to change the seat under a notice in flight.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public async Task<bool> SendAsync(PushSubscription to, string payload, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add((to, payload));
                if (_waiting.TryDequeue(out var waiting))
                {
                    _read++;
                    waiting.TrySetResult((to, payload));
                }
            }

            if (Hold is { } hold)
            {
                await hold.Task;
            }

            return Failing ? throw new HttpRequestException("The push service is out.") : !Gone;
        }

        public Task<(PushSubscription To, string Payload)> Next()
        {
            lock (Sent)
            {
                if (_read < Sent.Count)
                {
                    return Task.FromResult(Sent[_read++]);
                }

                var source = new TaskCompletionSource<(PushSubscription, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Enqueue(source);
                return source.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }
}
