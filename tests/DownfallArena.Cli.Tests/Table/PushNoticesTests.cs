using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

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
    private TableRegistry? _registry;
    private HostedTables? _hosted;

    public void Dispose()
    {
        _registry?.Dispose();
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
        notice.GetProperty("body").GetString().ShouldBe("Round 1: choose your mutations.");
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

    /// <summary>A subscription kept on a push service the host does not send to is forgotten as a gone one is (#1209).</summary>
    [Fact]
    public async Task A_subscription_on_a_push_service_the_host_does_not_send_to_is_forgotten_and_recorded()
    {
        var (table, notices) = await Seated();
        var recorded = new TaskCompletionSource<PlayedTable>(TaskCreationOptions.RunContinuationsAsynchronously);
        notices.Record = recordedTable => { recorded.TrySetResult(recordedTable); return Task.CompletedTask; };
        _sender.Gone = true;
        _sender.Refusal = PushSent.UnknownService;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        (await recorded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeSameAs(table);
        table.Seats[0].Push.ShouldBeNull();
        notices.IsDead(Phone.Endpoint).ShouldBeTrue();
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
        var seat = new TableSeat(Domain.Matches.PlayerSlot.Player2, "token", new SteppedPerson());

        var withRound = JsonDocument.Parse(PushNotices.Payload(new Asking(Application.Matches.Projections.PlayerOptionsKind.Speed, null, 4), 7, seat, "a b")).RootElement;
        var atTheSlot = JsonDocument.Parse(PushNotices.Payload(new Asking(Application.Matches.Projections.PlayerOptionsKind.Action, null, 5), null, seat, "t")).RootElement;

        withRound.GetProperty("url").GetString().ShouldBe("/play?table=a%20b");
        withRound.GetProperty("body").GetString().ShouldBe("Round 7: choose a tempo.");
        withRound.GetProperty("asked").GetInt64().ShouldBe(4);
        atTheSlot.GetProperty("body").GetString().ShouldBe("Your creature acts: choose a spell.");
        atTheSlot.GetProperty("tag").GetString().ShouldBe("turn:player2");
    }

    [Theory]
    [InlineData(3, "Round 4: choose a spell. 3 casts since your last visit.")]
    [InlineData(1, "Round 4: choose a spell. 1 cast since your last visit.")]
    [InlineData(0, "Round 4: choose a spell.")]
    [InlineData(null, "Round 4: choose a spell.")]
    public void The_body_says_how_many_spells_were_cast_since_the_last_visit(int? casts, string body)
    {
        var seat = new TableSeat(Domain.Matches.PlayerSlot.Player1, "token", new SteppedPerson());

        var notice = JsonDocument.Parse(PushNotices.Payload(new Asking(Application.Matches.Projections.PlayerOptionsKind.Action, null, 9), 4, seat, "t", casts)).RootElement;

        notice.GetProperty("body").GetString().ShouldBe(body);
    }

    [Fact]
    public async Task A_notice_sent_after_the_last_visit_and_some_casts_carries_the_count_in_its_body()
    {
        var (table, _) = await Seated();
        table.Seats[0].Push = Phone;
        await Arrive(table);
        var person = table.Seats[0].Person!;
        var marked = false;
        Asking? next = null;
        for (var attempt = 0; attempt < 1500 && next is null; attempt++)
        {
            var waiting = person.Waiting;
            if (waiting is null)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                continue;
            }

            if (waiting.Kind == PlayerOptionsKind.Intent && !marked)
            {
                var shown = (await ViewAsync(table)).GetProperty("feed").EnumerateArray().Max(entry => entry.GetProperty("sequence").GetInt32());
                (await table.Api.HandleAsync("PUT", "/api/seat/player1/seen", $$"""{ "sequence": {{shown}} }""", table.Seats[0].Token)).Status.ShouldBe(200);
                marked = true;
            }

            var casts = (await table.Api.SinceVisitAsync("player1")).Casts;
            if (marked && casts > 0 && waiting.Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.Speed)
            {
                next = waiting;
                continue;
            }

            await AnswerAsync(table, waiting);
        }

        next.ShouldNotBeNull("the seat is asked again after casts");
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var count = (await table.Api.SinceVisitAsync("player1")).Casts.ShouldNotBeNull();
        count.ShouldBeGreaterThan(0);
        _clock.Advance(Grace);

        var (_, payload) = await _sender.Next();

        JsonDocument.Parse(payload).RootElement.GetProperty("body").GetString()
            .ShouldEndWith(count == 1 ? "1 cast since your last visit." : $"{count} casts since your last visit.");
    }

    private static async Task<JsonElement> ViewAsync(PlayedTable table)
    {
        var served = await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token);
        return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(served.Body)).RootElement.Clone();
    }

    /// <summary>The first legal answer to the question, read off the options the seat is served, as a page would post it.</summary>
    private static async Task AnswerAsync(PlayedTable table, Asking waiting)
    {
        var options = (await ViewAsync(table)).GetProperty("options");
        var creature = waiting.Creature?.Value;
        var body = waiting.Kind switch
        {
            PlayerOptionsKind.Evolution => """{"kind":"Evolution","pass":true}""",
            PlayerOptionsKind.Speed => $$"""{"kind":"Speed","creature":{{creature}},"speed":"Quick"}""",
            PlayerOptionsKind.TieOrder => $$"""{"kind":"TieOrder","order":{{options.GetProperty("tieOrder").GetProperty("asRolled").GetRawText()}}}""",
            PlayerOptionsKind.Intent => $$"""{"kind":"Intent","creature":{{creature}},"spell":{{options.GetProperty("intent").GetProperty("creatures").EnumerateArray().First(one => one.GetProperty("creature").GetInt32() == creature).GetProperty("castableSpells")[0].GetRawText()}}}""",
            PlayerOptionsKind.Target => $$"""{"kind":"Target","creature":{{creature}},"targets":[{{options.GetProperty("target").GetProperty("legalTargets").GetProperty("candidates")[0].GetInt32()}}]}""",
            _ => throw new InvalidOperationException($"Unexpected question {waiting.Kind}."),
        };
        var answer = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", body.Insert(1, $"\"asked\":{waiting.Asked},"), table.Seats[0].Token);
        answer.Status.ShouldBe(204, System.Text.Encoding.UTF8.GetString(answer.Body));
    }

    [Fact]
    public async Task One_phone_holding_both_seats_is_hotseat_and_is_not_notified()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await Added(HostedTables.TwoPeople);
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

    [Fact]
    public async Task A_signed_in_seat_is_told_its_turn_on_its_account_s_devices_tagged_with_the_table()
    {
        var (table, notices) = await Seated();
        var account = AccountId.New();
        var devices = await DevicesAsync(account, "https://push.example/laptop");
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, devices));
        table.Seats[0].Account = account;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        var (to, payload) = await _sender.Next();
        to.Endpoint.ShouldBe("https://push.example/laptop");
        var notice = JsonDocument.Parse(payload).RootElement;
        notice.GetProperty("title").GetString().ShouldBe("Your turn");
        notice.GetProperty("body").GetString().ShouldBe("Round 1: choose your mutations.");
        notice.GetProperty("tag").GetString().ShouldBe($"turn:{table.Id}");
        await Settled();
        _sender.Sent.Count.ShouldBe(1, "the seat's own subscription is told only while the account holds it as a device");
    }

    [Fact]
    public async Task A_signed_in_seat_whose_account_turned_turns_off_is_told_nothing()
    {
        var (table, notices) = await Seated();
        var account = AccountId.New();
        var preferences = Substitute.For<IPreferenceStore>();
        preferences.GetNoticesAsync(account, Arg.Any<CancellationToken>()).Returns(new NoticePreferences(Turns: false, Invitations: true, Results: true));
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, await DevicesAsync(account, "https://push.example/laptop"))) { Preferences = preferences };
        table.Seats[0].Account = account;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        await Settled();
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_device_that_holds_the_other_seat_is_hotseat_and_is_not_told_the_signed_in_seat_s_turn()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await Added(HostedTables.TwoPeople);
        var account = AccountId.New();
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, await DevicesAsync(account, Phone.Endpoint)));
        table.Seats[0].Account = account;
        // The page of a phone holding both seats binds its one subscription to each (ADR 0120).
        table.Seats[0].Push = Phone;
        table.Seats[1].Push = Phone;

        await Arrive(table);
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", table.Seats[1].Token)).Status.ShouldBe(200);
        await Asked(table);
        _clock.Advance(Grace);

        await Settled();
        _sender.Sent.ShouldBeEmpty("the phone that holds the other seat is held by the other player");
    }

    [Fact]
    public async Task One_account_in_both_seats_is_hotseat_and_none_of_its_devices_is_told()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await Added(HostedTables.TwoPeople);
        var account = AccountId.New();
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, await DevicesAsync(account, "https://push.example/laptop")));
        table.Seats[0].Account = account;
        table.Seats[1].Account = account;

        await Arrive(table);
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", table.Seats[1].Token)).Status.ShouldBe(200);
        await Asked(table);
        _clock.Advance(Grace);

        await Settled();
        _sender.Sent.ShouldBeEmpty("the account's player holds both seats: nobody is away to be told");
    }

    /// <summary>A browser the account let go of, signed out or turned off, is not brought back by the seat subscription it once bound.</summary>
    [Fact]
    public async Task A_signed_in_seat_s_subscription_its_account_does_not_hold_is_not_told_nor_added_back()
    {
        var (table, notices) = await Seated();
        var account = AccountId.New();
        var devices = new AccountDevices(new InMemoryDeviceStore(), TimeProvider.System);
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, devices));
        table.Seats[0].Account = account;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        await Settled();
        _sender.Sent.ShouldBeEmpty();
        (await devices.ListAsync(account, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task With_a_queue_a_signed_in_seat_s_question_nobody_drew_waits_on_it_for_the_grace_and_is_told_when_it_comes_due()
    {
        var (table, notices) = await Seated();
        var (outbox, worker) = await QueuedAsync(notices, table);

        await Arrive(table);
        await Asked(table);
        await QueuedOnce(outbox);
        (await worker.RunOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(0, "hidden for the grace");
        _clock.Advance(Grace);
        await worker.RunOnceAsync(TestContext.Current.CancellationToken);

        var (to, payload) = await _sender.Next();
        to.Endpoint.ShouldBe(Laptop);
        JsonDocument.Parse(payload).RootElement.GetProperty("tag").GetString().ShouldBe($"turn:{table.Id}");
        (await outbox.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task With_a_queue_a_question_the_page_drew_in_time_is_taken_back_from_it()
    {
        var (table, notices) = await Seated();
        var (outbox, _) = await QueuedAsync(notices, table);

        await Arrive(table);
        var asked = await Asked(table);
        await QueuedOnce(outbox);
        (await table.Api.HandleAsync("GET", "/api/seat/player1", "", table.Seats[0].Token, null, $"?shown={asked}")).Status.ShouldBe(200);

        for (var attempt = 0; attempt < 250 && outbox.Bodies.Count > 0; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        outbox.Bodies.ShouldBeEmpty("taken back while still hidden, before the grace is over");
    }

    [Fact]
    public async Task A_turn_queued_by_a_process_that_stopped_is_told_by_the_next_one_and_not_once_answered()
    {
        var (table, notices) = await Seated();
        var (outbox, _) = await QueuedAsync(notices, table);
        await Arrive(table);
        var asked = await Asked(table);
        await QueuedOnce(outbox);

        // Another process, which never saw the asking, reads the item once it comes due.
        var next = new PushNotices(_sender, "BP8", _clock, Grace) { Accounts = notices.Accounts };
        var worker = new NoticeWorker(outbox, new InMemoryWorkChannel("notices-poison", _clock), _clock) { Turns = (due, _) => next.TurnDueAsync(table, due) };
        _clock.Advance(Grace);
        await worker.RunOnceAsync(TestContext.Current.CancellationToken);
        await next.TurnDueAsync(table, new DueTurn(table.Id, "player1", asked - 1, 1));

        var (to, _) = await _sender.Next();
        to.Endpoint.ShouldBe(Laptop);
        _sender.Sent.Count.ShouldBe(1, "an asking the seat is no longer on is owed nothing");
    }

    [Fact]
    public async Task With_a_queue_a_seat_without_an_account_still_waits_here_and_is_told_on_its_own_phone()
    {
        var (table, notices) = await Seated();
        var outbox = new InMemoryWorkChannel(QueuedNotice.Queue, _clock);
        notices.Outbox = outbox;
        table.Seats[0].Push = Phone;

        await Arrive(table);
        await Asked(table);
        _clock.Advance(Grace);

        (await _sender.Next()).To.ShouldBe(Phone);
        outbox.Bodies.ShouldBeEmpty("its 410 is heard where its page subscribed it");
    }

    private const string Laptop = "https://push.example/laptop";

    /// <summary>A queue for the notices, the first seat signed in with a laptop as its account's device, and a worker over the queue.</summary>
    private async Task<(InMemoryWorkChannel Outbox, NoticeWorker Worker)> QueuedAsync(PushNotices notices, PlayedTable table)
    {
        var account = AccountId.New();
        notices.Accounts = new AccountNotifier(log: null, new AccountNotices(_sender, await DevicesAsync(account, Laptop)));
        table.Seats[0].Account = account;
        var outbox = new InMemoryWorkChannel(QueuedNotice.Queue, _clock);
        notices.Outbox = outbox;
        return (outbox, new NoticeWorker(outbox, new InMemoryWorkChannel("notices-poison", _clock), _clock) { Turns = (due, _) => notices.TurnDueAsync(table, due) });
    }

    /// <summary>The asking's "your turn" on the queue, once the task that puts it there has.</summary>
    private static async Task QueuedOnce(InMemoryWorkChannel outbox)
    {
        for (var attempt = 0; attempt < 250 && outbox.Bodies.Count == 0; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        QueuedNotice.Parse(outbox.Bodies.ShouldHaveSingleItem())!.Turn!.Seat.ShouldBe("player1");
    }

    private static async Task<AccountDevices> DevicesAsync(AccountId account, string endpoint)
    {
        var devices = new AccountDevices(new InMemoryDeviceStore(), TimeProvider.System);
        await devices.AddAsync(account, new PushDevice(endpoint, InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), TestContext.Current.CancellationToken);
        return devices;
    }

    private async Task<(PlayedTable Table, PushNotices Notices)> Seated()
    {
        var notices = new PushNotices(_sender, "BP8", _clock, Grace);
        _hosted = new HostedTables(null, push: notices);
        var table = await Added(HostedTables.OnePerson);
        return (table, notices);
    }

    /// <summary>A table composed on the host and held by its registry.</summary>
    private async Task<PlayedTable> Added(TableRequest request)
    {
        var table = await _hosted!.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        _registry = _hosted.Registry();
        using var place = (await _registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, TestContext.Current.CancellationToken);
        return table;
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

        /// <summary>What a refused send answers: the push service's gone, or a service the host does not send to.</summary>
        public PushSent Refusal { get; set; } = PushSent.Gone;

        public bool Failing { get; set; }

        /// <summary>Set, the send waits on it before answering: what a test needs to change the seat under a notice in flight.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public async Task<PushSent> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
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

            return Failing ? throw new HttpRequestException("The push service is out.") : Gone ? Refusal : PushSent.Taken;
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
