using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Cli.Studio;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The Feedback tab (docs/tabletop/feedback-plan.md, R3, ADR 0189), behind the admin panel's gate.
/// <c>GET /api/admin/feedback?kind&amp;status&amp;category&amp;page</c> lists it, newest first, twenty a page;
/// <c>GET .../&lt;id&gt;</c> is one sheet, the position the player attached included; <c>PUT .../&lt;id&gt;/status
/// {"status","reason?"}</c> marks it seen, reproduced or dismissed (<c>"reason":"spam"</c> is the spam mark);
/// <c>PUT .../&lt;id&gt;/kind {"kind"}</c> turns a bug report into an idea or back; <c>POST</c> and <c>DELETE
/// .../&lt;id&gt;/mute</c> stop its sender from sending feedback for 30 days, and lift it. Each change is an Activity line.
/// </summary>
internal sealed class FeedbackAdminApi(OperatorFeedback feedback, StoredSessions? stored)
{
    public const string Route = "/api/admin/feedback";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>What operators do from the panel (A6): every change to feedback is noted with the rest.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public static bool Names(string path) => path == Route || path.StartsWith(Route + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string operatorName, string? query = null)
    {
        var parts = path[Route.Length..].Trim('/').Split('/');
        return (method, parts) switch
        {
            ("GET", [""]) => await ListAsync(System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)),
            ("GET", [var id]) => await SheetAsync(id),
            ("PUT", [var id, "status"]) => await StatusAsync(id, body, operatorName),
            ("PUT", [var id, "kind"]) => await KindAsync(id, body, operatorName),
            ("POST", [var id, "mute"]) => await MuteAsync(id, operatorName),
            ("DELETE", [var id, "mute"]) => await UnmuteAsync(id, operatorName),
            _ => Error("Feedback.NoRoute", $"No such route: {method} {path}", 404),
        };
    }

    private async Task<StudioResponse> ListAsync(System.Collections.Specialized.NameValueCollection asked)
    {
        FeedbackKind? kind = asked["kind"] switch { "bug" => FeedbackKind.Bug, "idea" => FeedbackKind.Idea, _ => null };
        var status = asked["status"];
        var filter = new FeedbackFilter(
            kind,
            status is { Length: > 0 } && status != "open" ? StatusOf(status) : null,
            asked["category"] is { Length: > 0 } category ? category : null,
            OpenOnly: status == "open");
        var page = await feedback.PageAsync(filter, int.TryParse(asked["page"], out var number) ? number : 1);
        return StudioResponse.OfJson(
            new
            {
                feedback = page.Rows.Select(one => new
                {
                    id = one.Id,
                    kind = Name(one.Kind),
                    category = one.Category,
                    line = FirstLine(one.Text),
                    status = Name(one.Status),
                    reason = one.Reason,
                    at = one.At,
                    table = one.Context?.Table,
                    scenario = one.Context?.Scenario,
                    round = one.Context?.Round,
                    additions = one.Additions.Count,
                }).ToArray(),
                total = page.Total,
                page = page.Page,
                pages = page.Pages,
                perPage = OperatorFeedback.PerPage,
            },
            Web);
    }

    private async Task<StudioResponse> SheetAsync(string id)
    {
        if (await feedback.FindAsync(id) is not { } one)
        {
            return Refused(FeedbackErrors.NotFound(id));
        }

        var mute = await feedback.MuteOfAsync(one);
        var table = one.Context?.Table;
        var record = table is null || stored is null ? null : await stored.RecordAsync(table);
        return StudioResponse.OfJson(
            new
            {
                id = one.Id,
                kind = Name(one.Kind),
                category = one.Category,
                status = Name(one.Status),
                reason = one.Reason,
                at = one.At,
                closedAt = one.ClosedAt,
                text = one.Text,
                expected = one.Expected,
                additions = one.Additions.Select(addition => new { at = addition.At, text = addition.Text, expected = addition.Expected }).ToArray(),
                page = one.Page,
                browser = one.Browser,
                errors = one.Errors.Select(error => new { message = error.Message, where = error.Where }).ToArray(),
                account = one.Account,
                guest = one.Account is null && one.Sender != ForgetFeedbackAccount.Gone,
                senderGone = one.Sender == ForgetFeedbackAccount.Gone,
                mute = mute is null ? null : new { at = mute.At, until = mute.Until, by = mute.By, why = mute.Why },
                context = one.Context is not { } context ? null : new
                {
                    table = context.Table,
                    seat = context.Seat,
                    round = context.Round,
                    subPhase = context.SubPhase,
                    journalLength = context.JournalLength,
                    content = context.Content,
                    rules = context.Rules?.ToString(),
                    engine = context.Engine,
                    scenario = context.Scenario,
                    practiceEntries = context.PracticeEntries?.Count,
                    position = context.Position,
                },
                session = table is null ? null : $"/session/{Uri.EscapeDataString(table)}",

                // Diagnose… is offered while the table it was sent from is still under way (scenarios plan, S5b).
                underWay = record is { Status: TableRecord.Open, Void: false },
            },
            Web);
    }

    private async Task<StudioResponse> StatusAsync(string id, string body, string operatorName)
    {
        var asked = Read<StatusBody>(body);
        if (asked?.Status is not { } named || StatusOf(named) is not { } status)
        {
            return Refused(FeedbackErrors.Invalid("Name a status: seen, reproduced or dismissed."));
        }

        var changed = await feedback.SetStatusAsync(id, status, asked.Reason, operatorName);
        if (changed.IsFailure)
        {
            return Refused(changed.Error);
        }

        var (kept, muted) = (changed.Value.Feedback, changed.Value.Muted);
        Console.WriteLine($"  Feedback {id} marked {Name(kept.Status)}{(kept.Reason is null ? string.Empty : $" ({kept.Reason})")} by {operatorName}.");
        if (kept.IsSpam)
        {
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackSpam, id);
        }
        else
        {
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackStatus, id, kept.Reason is null ? Name(kept.Status) : $"{Name(kept.Status)}: {kept.Reason}");
        }

        if (muted is not null)
        {
            Console.WriteLine($"  Feedback from the sender of {id} muted until {muted.Until:u}: {FeedbackMute.SpamMarks} marked as spam in {FeedbackMute.SpamWindow.TotalDays:0} days.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackMute, id, $"{FeedbackMute.SpamMarks} marked as spam in {FeedbackMute.SpamWindow.TotalDays:0} days");
        }

        return await SheetAsync(id);
    }

    private async Task<StudioResponse> KindAsync(string id, string body, string operatorName)
    {
        FeedbackKind? kind = Read<KindBody>(body)?.Kind switch { "bug" => FeedbackKind.Bug, "idea" => FeedbackKind.Idea, _ => null };
        if (kind is null)
        {
            return Refused(FeedbackErrors.Invalid("Name a kind: bug or idea."));
        }

        var before = await feedback.FindAsync(id);
        var changed = await feedback.ChangeKindAsync(id, kind.Value);
        if (changed.IsFailure)
        {
            return Refused(changed.Error);
        }

        if (before is not null && before.Kind != kind)
        {
            Console.WriteLine($"  Feedback {id} turned into {(kind == FeedbackKind.Bug ? "a bug report" : "an idea")} by {operatorName}.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackConvert, id, $"{Name(before.Kind)} → {Name(kind.Value)}");
        }

        return await SheetAsync(id);
    }

    private async Task<StudioResponse> MuteAsync(string id, string operatorName)
    {
        var muted = await feedback.MuteAsync(id, operatorName);
        if (muted.IsFailure)
        {
            return Refused(muted.Error);
        }

        Console.WriteLine($"  Feedback from the sender of {id} muted until {muted.Value.Until:u} by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.FeedbackMute, id, $"{FeedbackMute.Lasts.TotalDays:0} days");
        return await SheetAsync(id);
    }

    private async Task<StudioResponse> UnmuteAsync(string id, string operatorName)
    {
        var lifted = await feedback.UnmuteAsync(id);
        if (lifted.IsFailure)
        {
            return Refused(lifted.Error);
        }

        Console.WriteLine($"  Feedback from the sender of {id} let through again by {operatorName}.");
        await Notes.NoteAsync(operatorName, OperatorActions.FeedbackUnmute, id);
        return await SheetAsync(id);
    }

    /// <summary>A status named as the routes name it, lowercase; none for a word no status is.</summary>
    private static FeedbackStatus? StatusOf(string named) =>
        Enum.GetValues<FeedbackStatus>().Select(status => (FeedbackStatus?)status).FirstOrDefault(status => Name(status!.Value) == named);

    private static string Name(FeedbackStatus status) => status.ToString().ToLowerInvariant();

    private static string Name(FeedbackKind kind) => kind == FeedbackKind.Bug ? "bug" : "idea";

    /// <summary>What a row shows of the words: their first line, cut at 120 characters.</summary>
    private static string FirstLine(string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        return line.Length <= 120 ? line : $"{line[..119]}…";
    }

    private static T? Read<T>(string body)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static StudioResponse Refused(DomainError error) => Error(error.Code, error.Message, error.Code switch
    {
        "Feedback.NotFound" => 404,
        "Feedback.Closed" or "Feedback.NoSender" => 409,
        _ => 400,
    });

    private static StudioResponse Error(string code, string message, int status) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status: status);

    private sealed record StatusBody(string? Status, string? Reason);

    private sealed record KindBody(string? Kind);
}
