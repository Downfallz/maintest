using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// "Your turn", sent to a seat's phone when the match asks it something and nobody is looking (ADR 0118). A
/// page in front of somebody says so once it drew it (<c>?shown=</c>, <see cref="DecisionClock" />), and a seat
/// whose page said so gets no notice; a seat whose page is asleep, closed or on another app gets one after a
/// short grace, and its service worker shows it. One notice an asking at most: the next asking comes only
/// after this one is answered.
/// </summary>
internal sealed class PushNotices(IPushSender sender, string publicKey, TimeProvider clock, TimeSpan? grace = null)
{
    /// <summary>
    /// How long a question waits for its page before a notice goes. A page in view is told of the question by its
    /// stream (ADR 0119), reads it at once and acknowledges it once drawn (ADR 0164), so two seconds is a page that
    /// is not there, not one that is slow.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    /// <summary>One topic a seat: a phone switched back on reads one "your turn", the latest (<see cref="IPushSender" />).</summary>
    public const string Topic = "turn";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly TimeSpan _grace = grace ?? Grace;

    // The last asking each seat's page said it drew, kept on the seat's own lifetime: a table let go of takes
    // its seats, and their entries, with it.
    private readonly ConditionalWeakTable<ISeatPerson, StrongBox<long>> _shown = [];

    // The endpoints a push service said were gone. A browser keeps its subscription when the service drops it,
    // and a page sends it again on every load; answering those with 410 is how the page learns to make a new
    // one. Small: one line per subscription that died, for the life of the host.
    private readonly ConcurrentDictionary<string, byte> _dead = new(StringComparer.Ordinal);

    /// <summary>What a page subscribes with: the host's VAPID public key, base64url.</summary>
    public string PublicKey { get; } = publicKey;

    /// <summary>
    /// Writes a table's record down, for a subscription the push service says is gone: the next host must not
    /// send to it either. Set by the host once it has the codes a record carries.
    /// </summary>
    public Func<PlayedTable, Task>? Record { get; set; }

    /// <summary>
    /// The door of every notice to an account (ADR 0201): a seat that belongs to an account is told its turn on the
    /// account's devices through it, as the account chose (ADR 0202), rather than on the seat's own subscription.
    /// Set by the host that keeps accounts; none on a host without them.
    /// </summary>
    public AccountNotifier? Accounts { get; set; }

    /// <summary>
    /// The notices queue (ADR 0203): a question nobody drew is put on it, hidden for the grace, and taken back when the
    /// seat's page draws it in time, so a process that stops meanwhile loses no notice; <see cref="NoticeWorker" /> tells
    /// the seat once it comes due (<see cref="TurnDueAsync" />). None on a host with no queue, which waits in its own
    /// process, as before.
    /// </summary>
    public IWorkChannel? Outbox { get; set; }

    // The "your turn" each seat has on the queue, still hidden, so its page drawing the question takes it back.
    private readonly ConditionalWeakTable<ISeatPerson, StrongBox<QueuedWork?>> _queued = [];

    /// <summary>Every seat a person holds at this table, from now on: whatever the match asks it is noticed.</summary>
    public void Watch(PlayedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        foreach (var seat in table.Seats)
        {
            if (seat.Person is { } person)
            {
                person.Asked += (question, round) => _ = NoticeAsync(table, seat, question, round);
            }
        }
    }

    /// <summary>A page has drawn this asking in front of somebody: no notice for it.</summary>
    public void Shown(TableSeat seat, Asking question)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(question);
        if (seat.Person is not { } person)
        {
            return;
        }

        var shown = _shown.GetOrCreateValue(person);
        lock (shown)
        {
            shown.Value = Math.Max(shown.Value, question.Asked);
        }

        _ = TakeBackAsync(person, question.Asked);
    }

    /// <summary>
    /// "Your turn" come due on the notices queue (ADR 0203), for a seat that belongs to an account: told on the
    /// account's devices unless the table is over, the seat was answered or asked something else meanwhile, or its page
    /// here drew it. A failure is thrown, for the queue to try again.
    /// </summary>
    public async Task TurnDueAsync(PlayedTable table, DueTurn due)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(due);
        if (table.IsOver || table.Seats.FirstOrDefault(seat => string.Equals(seat.Name, due.Seat, StringComparison.Ordinal)) is not { Person: { Waiting: { } question } person } seat || question.Asked != due.Asked)
        {
            return;
        }

        if ((_shown.TryGetValue(person, out var shown) && shown.Value >= question.Asked) || seat.Account is not { } account || Accounts is not { } door)
        {
            return;
        }

        await NoticeAccountAsync(door, account, table, seat, question, due.Round);
    }

    // The seat's "your turn" still hidden on the queue, when it is for this asking or an earlier one: taken back, since
    // the page drew the question. One that cannot be taken back comes due, and the table says nothing is owed.
    private async Task TakeBackAsync(ISeatPerson person, long asked)
    {
        if (Outbox is not { } outbox || !_queued.TryGetValue(person, out var box))
        {
            return;
        }

        QueuedWork? queued;
        lock (box)
        {
            queued = box.Value;
            if (queued is null || DueTurnOf(queued)?.Asked > asked)
            {
                return;
            }

            box.Value = null;
        }

        try
        {
            await outbox.DeleteAsync(queued);
        }
        catch (Azure.RequestFailedException gone) when (gone.Status == 404 || gone.ErrorCode == "PopReceiptMismatch")
        {
            // Already received by a worker, here or in table-worker: it has come due, and is told or found owed nothing.
        }
        catch (Exception failure)
        {
            Console.WriteLine($"  A \"your turn\" drawn in time could not be taken back from {outbox.Name}, so it may still be sent: {HttpHost.OneLine(failure)}");
        }
    }

    private static DueTurn? DueTurnOf(QueuedWork work) => QueuedNotice.Parse(work.Body)?.Turn;

    /// <summary>Whether a push service said this endpoint is gone: a subscription the page should not send again.</summary>
    public bool IsDead(string endpoint) => _dead.ContainsKey(endpoint);

    /// <summary>
    /// A notice sent now, on request, with no grace and whatever the page is doing: what a player taps to see
    /// whether this phone can be reached at all, and what the push service answers when it cannot. The worker
    /// shows it even over a visible game, since it is the one notice that is the point.
    /// </summary>
    public async Task<PushTest> TestAsync(PlayedTable table, TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(seat);
        if (seat.Push is not { } subscription)
        {
            return new PushTest(false, "This seat has no subscription: tap \"Notify me when it's my turn\" first.");
        }

        var payload = JsonSerializer.Serialize(new { title = "Notifications work", body = "This phone is told when it is your turn.", url = $"/play?table={Uri.EscapeDataString(table.Id)}", tag = $"test:{seat.Name}", test = true }, Web);
        try
        {
            var sent = await sender.SendAsync(subscription, payload, Topic);
            if (sent is PushSent.Taken)
            {
                Console.WriteLine($"  Session {table.Id}: {seat.Name} sent a test notice.");
                return new PushTest(true, "Sent. If nothing showed, check the phone's notification settings for this browser.");
            }

            // Only the subscription that was refused: the seat may hold another by now (NoticeAsync says why).
            _dead[subscription.Endpoint] = 0;
            if (seat.Push == subscription)
            {
                seat.Push = null;
                if (Record is { } record)
                {
                    await record(table);
                }
            }

            // A subscription kept from before the host checked push services is never sent to. Tapping again would be
            // refused on that service too, so answer with that refusal, not "gone" (#1176, #1209).
            return sent is PushSent.UnknownService
                ? PushTest.OnUnknownService
                : new PushTest(false, PushTest.SeatTestGoneMessage);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Azure.RequestFailedException or System.Security.Cryptography.CryptographicException)
        {
            Console.WriteLine($"  Session {table.Id}: {seat.Name} could not be sent a test notice: {HttpHost.OneLine(failure)}");
            return new PushTest(false, PushTest.Refused(failure, PushTest.SeatAgain));
        }
    }

    /// <summary>
    /// What the service worker is handed: a title, a line, and where a tap goes, which is the game at this table:
    /// a browser may hold several (ADR 0115), and the question is at the one that asked. The id is the session's
    /// public name, on the session page's address already; no token travels.
    /// </summary>
    /// <remarks>
    /// <paramref name="casts" /> is how many spells the seat may see were cast since its last visit (async recap plan,
    /// R4): said after the question when there were any, so the player knows what waits before they open the game.
    /// </remarks>
    public static string Payload(Asking question, int? round, TableSeat seat, string table, int? casts = null)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(table);
        return JsonSerializer.Serialize(new { title = "Your turn", body = Body(question, round, casts), url = $"/play?table={Uri.EscapeDataString(table)}", tag = $"turn:{seat.Name}", asked = question.Asked }, Web);
    }

    /// <summary>
    /// "Your turn" to the account a seat belongs to: tagged with the table, since an account's devices hold every table
    /// it plays, and replaced on the phone by the warning that the same turn is about to time out (<see cref="TurnTimeouts" />).
    /// </summary>
    public static Notice AccountNotice(Asking question, int? round, string table, int? casts = null)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(table);
        return new Notice(NoticeKind.Turn, "Your turn", Body(question, round, casts), $"/play?table={Uri.EscapeDataString(table)}", $"turn:{table}");
    }

    private static string Body(Asking question, int? round, int? casts)
    {
        var what = question.Kind switch
        {
            PlayerOptionsKind.Evolution => "choose your mutations",
            PlayerOptionsKind.Speed => "choose a tempo",
            PlayerOptionsKind.TieOrder => "order the tie",
            PlayerOptionsKind.Intent => "declare a spell",
            PlayerOptionsKind.Target => "choose your targets",
            PlayerOptionsKind.Action => "choose a spell",
            _ => "decide",
        };
        var body = round is { } number ? string.Create(CultureInfo.InvariantCulture, $"Round {number}: {what}.") : $"Your creature acts: {what}.";
        if (casts is > 0)
        {
            body += string.Create(CultureInfo.InvariantCulture, $" {casts} {(casts == 1 ? "cast" : "casts")} since your last visit.");
        }

        return body;
    }

    // The count is a line added to the notice, never a reason to lose it: whatever its read throws, the notice goes without it.
    private static async Task<int?> CastsAsync(PlayedTable table, TableSeat seat)
    {
        try
        {
            return (await table.Api.SinceVisitAsync(seat.Name, withLastMove: false)).Casts;
        }
        catch (Exception failure)
        {
            Console.WriteLine($"  Session {table.Id}: the casts since {seat.Name}'s last visit could not be counted: {failure.Message}");
            return null;
        }
    }

    // A signed-in seat is told on its account's devices (ADR 0202), as the account chose, and only those: a browser the
    // account let go of (turned off, signed out) is not brought back by the seat subscription it once bound. Never on a
    // phone that holds the other seat: one account in both seats, or a device the other seat's page registered, is hotseat (ADR 0120).
    private static async Task NoticeAccountAsync(AccountNotifier door, AccountId account, PlayedTable table, TableSeat seat, Asking question, int? round)
    {
        var others = table.Seats.Where(other => other != seat).ToList();
        if (others.Any(other => other.Account == account))
        {
            return;
        }

        var except = others.Where(other => other.Push is not null).Select(other => other.Push!.Endpoint).ToList();
        var reached = await door.NotifyAsync(account, AccountNotice(question, round, table.Id, await CastsAsync(table, seat)), except);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  Session {table.Id}: {seat.Name} notified of asking {question.Asked} on {reached} of its account's devices."));
    }

    private async Task NoticeAsync(PlayedTable table, TableSeat seat, Asking question, int? round)
    {
        try
        {
            // Only a seat that belongs to an account waits on the queue: it is told on the account's devices, whose
            // gone endpoints every process reads (ADR 0203). A seat without one keeps ADR 0118's wait here, where the
            // page that subscribed it is answered 410.
            if (Outbox is { } outbox && seat.Account is not null && Accounts is not null && seat.Person is { } asked && await QueuedAsync(outbox, table, seat, asked, question, round))
            {
                return;
            }

            await Task.Delay(_grace, clock);
            if (table.IsOver || seat.Person?.Waiting != question)
            {
                return;
            }

            if (seat.Person is { } person && _shown.TryGetValue(person, out var shown) && shown.Value >= question.Asked)
            {
                return;
            }

            await TellAsync(table, seat, question, round);
        }
        catch (Exception failure)
        {
            // The end of a task nothing awaits: whatever failed -- a service that is out, a record that cannot
            // be written, a key the sender could not use -- the notice is lost, the seat keeps its subscription
            // for the next asking, and the console says what happened rather than nothing.
            Console.WriteLine($"  Session {table.Id}: {seat.Name} could not be notified: {failure.Message}");
        }
    }

    // On the queue, hidden for the grace, and held to be taken back if the page draws the question first, even while
    // the item was being sent.
    // A queue that is out is no reason to lose the notice: it waits in this process instead, as on a host with no queue.
    private async Task<bool> QueuedAsync(IWorkChannel outbox, PlayedTable table, TableSeat seat, ISeatPerson person, Asking question, int? round)
    {
        QueuedWork queued;
        try
        {
            queued = await outbox.SendAsync(QueuedNotice.Of(new DueTurn(table.Id, seat.Name, question.Asked, round)).ToJson(), AccountNotices.KeptFor, _grace);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  Session {table.Id}: {seat.Name}'s turn could not be put on {outbox.Name}, so it waits here: {HttpHost.OneLine(failure)}");
            return false;
        }

        var box = _queued.GetOrCreateValue(person);
        lock (box)
        {
            box.Value = queued;
        }

        if (_shown.TryGetValue(person, out var shown) && shown.Value >= question.Asked)
        {
            await TakeBackAsync(person, question.Asked);
        }

        return true;
    }

    /// <summary>The seat told its turn, now: on its account's devices when it has one, or on its own subscription.</summary>
    private async Task TellAsync(PlayedTable table, TableSeat seat, Asking question, int? round)
    {
        if (seat.Account is { } account && Accounts is { } door)
        {
            await NoticeAccountAsync(door, account, table, seat, question, round);
            return;
        }

        if (seat.Push is not { } subscription)
        {
            return;
        }

        // One phone holding both seats is hotseat (ADR 0120): the other player is the person holding it, and a
        // "your turn" for either seat would only say what the pass screen says, twice an opportunity.
        if (table.Seats.Any(other => other != seat && other.Push?.Endpoint == subscription.Endpoint))
        {
            return;
        }

        if (await sender.SendAsync(subscription, Payload(question, round, seat, table.Id, await CastsAsync(table, seat)), Topic) is PushSent.Taken)
        {
            // Said, so a host's log shows what reached a phone and what did not.
            Console.WriteLine($"  Session {table.Id}: {seat.Name} notified of asking {question.Asked}.");
            return;
        }

        // Gone from the push service: the browser was reinstalled, or the service let it go. Sending again
        // would only fail again, so the seat forgets it and the endpoint is remembered as dead: the browser
        // still holds it and sends it on its next load, and is answered 410, which is when it makes a new one.
        // Only the subscription that was refused: the seat may hold another by now, the browser's new one or
        // the next player's, and that one answered nothing.
        _dead[subscription.Endpoint] = 0;
        if (seat.Push != subscription)
        {
            return;
        }

        seat.Push = null;
        if (Record is { } record)
        {
            await record(table);
        }
    }
}
