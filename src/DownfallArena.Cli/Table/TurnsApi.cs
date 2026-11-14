using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The bots' turn queues in the operator's Jobs tab (admin plan, A3b): each queue's length and oldest item, the dead
/// letters, and putting a letter's turn back on the queue or dropping it. Reached through <see cref="AdminApi" />,
/// behind its gate; on a host whose turns are taken in its own process there are no queues, and none of this.
/// </summary>
internal sealed class TurnsApi(TurnQueues queues)
{
    /// <summary>What operators do here (A6), noted beside each action's console line.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public const string Prefix = JobsApi.Prefix + "/turns";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            return (method, path[Prefix.Length..].Trim('/')) switch
            {
                ("GET", "") => await ListAsync(),
                ("POST", "requeue") => await RequeueAsync(body, operatorName),
                ("POST", "drop") => await DropAsync(body, operatorName),
                _ => Problem(404, "Turns.NoRoute", $"No such route: {method} {path}"),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "Turns.BadRequest", "The request is not valid JSON.");
        }
    }

    /// <summary>The first line of an error, which is what a list has room for.</summary>
    public static string FirstLine(string? text) => (text ?? string.Empty).Split('\n', 2)[0].TrimEnd('\r');

    private async Task<StudioResponse> ListAsync()
    {
        var states = await queues.StatesAsync();
        var letters = await queues.LettersAsync();
        return StudioResponse.OfJson(
            new
            {
                queues = states.Select(state => new { state.Name, state.Count, state.Oldest }).ToArray(),
                letters = letters.Select(listed => new
                {
                    listed.Id,
                    listed.Table,
                    queue = listed.Letter?.Queue,
                    delivered = listed.Letter?.Delivered,
                    error = FirstLine(listed.Letter?.Error ?? "Not a dead letter: kept as it was."),
                    fullError = listed.Letter?.Error,
                    queuedAt = listed.Letter?.QueuedAt,
                    poisonedAt = listed.Letter?.PoisonedAt,
                    deadLetteredAt = listed.Letter?.DeadLetteredAt,
                    host = listed.Letter?.Host,
                    body = listed.Letter?.Body ?? listed.Raw,
                }).ToArray(),
                shownAtMost = TurnQueues.MostShown,
            },
            Web);
    }

    private async Task<StudioResponse> RequeueAsync(string body, string operatorName)
    {
        var asked = Read(body);
        if (asked?.All != true && asked?.Ids is not { Length: > 0 })
        {
            return Problem(400, "Turns.NothingAsked", "Say which letters to requeue, by their ids, or all of them.");
        }

        var handled = await queues.RequeueAsync(asked.All == true ? null : asked.Ids);
        Console.WriteLine($"  Dead letters requeued by {operatorName}: {handled.Handled.Count}{Missing(handled)}.");
        await Notes.NoteAsync(operatorName, OperatorActions.Requeue, target: null, Letters(handled));
        return Answer(handled);
    }

    private async Task<StudioResponse> DropAsync(string body, string operatorName)
    {
        if (Read(body)?.Ids is not { Length: > 0 } ids)
        {
            return Problem(400, "Turns.NothingAsked", "Say which letters to drop, by their ids.");
        }

        var handled = await queues.DropAsync(ids);
        Console.WriteLine($"  Dead letters dropped by {operatorName}: {handled.Handled.Count}{Missing(handled)}.");
        await Notes.NoteAsync(operatorName, OperatorActions.Drop, target: null, Letters(handled));
        return Answer(handled);
    }

    // Which letters, by their ids, kept in the one line the operator log notes (A6): a dropped letter is gone from its
    // queue, so the log is the only place that still says which it was. One line, not one a letter, so that requeueing
    // them all does not push the rest of the log past what it keeps.
    private static string Letters(LettersHandled handled) =>
        $"{handled.Handled.Count} letter{(handled.Handled.Count == 1 ? string.Empty : "s")}{Missing(handled)}{(handled.Handled.Count == 0 ? string.Empty : $": {string.Join(", ", handled.Handled)}")}";

    private static string Missing(LettersHandled handled) =>
        handled.Missing.Count == 0 ? string.Empty : $", {handled.Missing.Count} no longer there";

    private static StudioResponse Answer(LettersHandled handled) =>
        StudioResponse.OfJson(new { handled = handled.Handled, missing = handled.Missing }, Web);

    private static LettersBody? Read(string body) =>
        string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<LettersBody>(body, Web);

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record LettersBody(string[]? Ids, bool? All);
}
