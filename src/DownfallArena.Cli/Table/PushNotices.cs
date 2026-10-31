using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// "Your turn", sent to a seat's phone when the match asks it something and nobody is looking (ADR 0117). A
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
    private readonly ConcurrentDictionary<(string Table, PlayerSlot Slot), long> _shown = new();

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
    public void Shown(PlayedTable table, TableSeat seat, HumanSeat.Question question)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(question);
        _shown.AddOrUpdate((table.Id, seat.Slot), question.Asked, (_, asked) => Math.Max(asked, question.Asked));
    }

    /// <summary>What the service worker is handed: a title, a line, and where a tap goes.</summary>
    public static string Payload(HumanSeat.Question question, int? round, TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(seat);
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
        return JsonSerializer.Serialize(new { title = "Your turn", body, url = "/play", tag = $"turn:{seat.Name}", asked = question.Asked }, Web);
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

            if (_shown.TryGetValue((table.Id, seat.Slot), out var shown) && shown >= question.Asked)
            {
                return;
            }

            if (await sender.SendAsync(subscription, Payload(question, round, seat)))
            {
                return;
            }

            // Gone from the push service: the browser unsubscribed, or was reinstalled. Sending again would only
            // fail again, so the seat forgets it; the page offers the button again on its next visit.
            seat.Push = null;
            if (Record is { } record)
            {
                await record(table);
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or Azure.RequestFailedException)
        {
            // A service that is out, or a record that cannot be written: the notice is lost, the seat keeps its
            // subscription for the next asking, and the console says what happened.
            Console.WriteLine($"  Session {table.Id}: {seat.Name} could not be notified: {failure.Message}");
        }
    }
}
