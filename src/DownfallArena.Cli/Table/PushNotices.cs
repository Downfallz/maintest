using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// "Your turn", sent to a seat's phone when the match asks it something and nobody is looking (ADR 0118). A
/// page in front of somebody says so within a poll (<c>?shown=</c>, <see cref="DecisionClock" />), and a seat
/// whose page said so gets no notice; a seat whose page is asleep, closed or on another app gets one after a
/// short grace, and its service worker shows it. One notice an asking at most: the next asking comes only
/// after this one is answered.
/// </summary>
internal sealed class PushNotices(IPushSender sender, string publicKey, TimeProvider clock, TimeSpan? grace = null)
{
    /// <summary>
    /// How long a question waits for its page before a notice goes. A page polls every 700 ms and acknowledges
    /// the asking it draws on the poll after, so two seconds is a page that is not there, not one that is slow.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly TimeSpan _grace = grace ?? Grace;

    // The last asking each seat's page said it drew, kept on the seat's own lifetime: a table let go of takes
    // its seats, and their entries, with it.
    private readonly ConditionalWeakTable<HumanSeat, StrongBox<long>> _shown = [];

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
    public void Shown(TableSeat seat, HumanSeat.Question question)
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
    }

    /// <summary>Whether a push service said this endpoint is gone: a subscription the page should not send again.</summary>
    public bool IsDead(string endpoint) => _dead.ContainsKey(endpoint);

    /// <summary>
    /// What the service worker is handed: a title, a line, and where a tap goes, which is the game at this table:
    /// a browser may hold several (ADR 0115), and the question is at the one that asked. The id is the session's
    /// public name, on the session page's address already; no token travels.
    /// </summary>
    public static string Payload(HumanSeat.Question question, int? round, TableSeat seat, string table)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(table);
        var what = question.Kind switch
        {
            PlayerOptionsKind.Evolution => "pick your packages",
            PlayerOptionsKind.Speed => "choose a speed",
            PlayerOptionsKind.TieOrder => "order the tie",
            PlayerOptionsKind.Intent => "declare a spell",
            PlayerOptionsKind.Target => "choose your targets",
            PlayerOptionsKind.Action => "choose a spell",
            _ => "decide",
        };
        var body = round is { } number ? string.Create(CultureInfo.InvariantCulture, $"Round {number}: {what}.") : $"Your creature acts: {what}.";
        return JsonSerializer.Serialize(new { title = "Your turn", body, url = $"/play?table={Uri.EscapeDataString(table)}", tag = $"turn:{seat.Name}", asked = question.Asked }, Web);
    }

    private async Task NoticeAsync(PlayedTable table, TableSeat seat, HumanSeat.Question question, int? round)
    {
        try
        {
            await Task.Delay(_grace, clock);
            if (seat.Push is not { } subscription || table.IsOver || seat.Person?.Waiting != question)
            {
                return;
            }

            if (seat.Person is { } person && _shown.TryGetValue(person, out var shown) && shown.Value >= question.Asked)
            {
                return;
            }

            if (await sender.SendAsync(subscription, Payload(question, round, seat, table.Id)))
            {
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
        catch (Exception failure)
        {
            // The end of a task nothing awaits: whatever failed -- a service that is out, a record that cannot
            // be written, a key the sender could not use -- the notice is lost, the seat keeps its subscription
            // for the next asking, and the console says what happened rather than nothing.
            Console.WriteLine($"  Session {table.Id}: {seat.Name} could not be notified: {failure.Message}");
        }
    }
}
