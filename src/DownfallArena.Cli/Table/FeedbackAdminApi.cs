using System.Text.Json;
using DownfallArena.Application.Accounts;
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
/// .../&lt;id&gt;/mute</c> stop its sender from sending feedback for 30 days, and lift it. Each change is an Activity line;
/// asked again, a change writes and notes nothing. The sheet says which of its actions apply, so the page decides none.
/// </summary>
internal sealed class FeedbackAdminApi(OperatorFeedback feedback, StoredSessions? stored, TimeProvider clock)
{
    public const string Route = "/api/admin/feedback";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>What operators do from the panel (A6): every change to feedback is noted with the rest.</summary>
    public OperatorNotes Notes { get; set; } = OperatorNotes.None;

    public static bool Names(string path) => path == Route || path.StartsWith(Route + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string operatorName, string? query = null)
    {
        try
        {
            return await RouteAsync(method, path, body, operatorName, query);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException or Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The feedback could not be read or written: {failure.Message}");
            return Error(FeedbackErrors.Unavailable.Code, FeedbackErrors.Unavailable.Message, 503);
        }
    }

    private async Task<StudioResponse> RouteAsync(string method, string path, string body, string operatorName, string? query)
    {
        var parts = path[Route.Length..].Trim('/').Split('/');
        return (method, parts) switch
        {
            ("GET", [""]) => await ListAsync(System.Web.HttpUtility.ParseQueryString(query ?? string.Empty)),
            ("GET", [var id]) => await SheetAsync(id),
            ("PUT", [var id, "status"]) => await StatusAsync(id, body, operatorName),
            ("PUT", [var id, "kind"]) => await KindAsync(id, body, operatorName),
            ("POST", [var id, "merge"]) => await MergeAsync(id, body, operatorName),
            ("POST", [var id, "mute"]) => await MuteAsync(id, operatorName),
            ("DELETE", [var id, "mute"]) => await UnmuteAsync(id, operatorName),
            _ => Error("Feedback.NoRoute", $"No such route: {method} {path}", 404),
        };
    }

    private async Task<StudioResponse> ListAsync(System.Collections.Specialized.NameValueCollection asked)
    {
        // A filter that names nothing the host knows is refused, not read as no filter: a typo would list everything.
        var (kind, status, category) = (asked["kind"] ?? string.Empty, asked["status"] ?? string.Empty, asked["category"] ?? string.Empty);
        if ((kind.Length > 0 && KindOf(kind) is null) || (status.Length > 0 && status != "open" && StatusOf(status) is null)
            || (category.Length > 0 && !SendFeedback.BugCategories.Concat(SendFeedback.IdeaPlaces).Contains(category, StringComparer.Ordinal)))
        {
            return Refused(FeedbackErrors.Invalid("Filter by a kind (bug, idea), a status or open, and a category the sheet offers."));
        }

        var filter = new FeedbackFilter(KindOf(kind), status is { Length: > 0 } and not "open" ? StatusOf(status) : null, category.Length > 0 ? category : null, OpenOnly: status == "open");
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
                    spam = one.IsSpam,
                    at = one.At,
                    table = one.Context?.Table,
                    scenario = one.Context?.Scenario,
                    round = one.Context?.Round,
                    additions = one.Additions.Count,
                    plan = one.Plan,
                    askedBy = page.Asks.GetValueOrDefault(one.Id)?.AskedBy,
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
        var group = await feedback.GroupAsync(one);
        var counted = one.Kind == FeedbackKind.Idea && one.MergedInto is null;
        var table = one.Context?.Table;
        var record = table is null || stored is null ? null : await stored.RecordAsync(table);
        var underWay = record is { Status: TableRecord.Open, Void: false };
        var now = clock.GetUtcNow();
        return StudioResponse.OfJson(
            new
            {
                id = one.Id,
                kind = Name(one.Kind),
                category = one.Category,
                status = Name(one.Status),
                reason = one.Reason,
                spam = one.IsSpam,
                at = one.At,
                closedAt = one.ClosedAt,
                text = one.Text,
                expected = one.Expected,
                additions = Additions(one),
                plan = one.Plan,

                // Ideas grouped (I1): how many asked, the ideas merged into this one, or the one it was merged into, which
                // counts it.
                askedBy = counted ? group.Asks.AskedBy : (int?)null,
                since = counted ? group.Asks.Since : (DateTimeOffset?)null,
                asks = group.Merged.Select(ask => new
                {
                    id = ask.Id,
                    at = ask.At,
                    category = ask.Category,
                    text = ask.Text,
                    expected = ask.Expected,
                    additions = Additions(ask),
                    account = ask.Account,
                    guest = ask.Account is null && ask.Sender != ForgetFeedbackAccount.Gone,
                    senderGone = ask.Sender == ForgetFeedbackAccount.Gone,
                }).ToArray(),
                mergedInto = group.Into is not { } into ? null : new { id = into.Id, line = FirstLine(into.Text), status = Name(into.Status) },
                page = one.Page,
                browser = one.Browser,
                errors = one.Errors.Select(error => new { message = error.Message, where = error.Where }).ToArray(),
                account = one.Account,
                guest = one.Account is null && one.Sender != ForgetFeedbackAccount.Gone,
                senderGone = one.Sender == ForgetFeedbackAccount.Gone,
                mute = mute is null ? null : new { at = mute.At, until = mute.Until, by = mute.By, why = mute.Why, daysLeft = (int)Math.Ceiling((mute.Until - now).TotalDays) },
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
                underWay,
                actions = Actions(one, mute, underWay, group),
            },
            Web);
    }

    private async Task<StudioResponse> StatusAsync(string id, string body, string operatorName)
    {
        var asked = Read<StatusBody>(body);
        if (asked?.Status is not { } named || StatusOf(named) is not { } status)
        {
            return Refused(FeedbackErrors.Invalid("Name a status: seen, reproduced or dismissed; for an idea planned, shipped or declined."));
        }

        var changed = status == FeedbackStatus.Planned
            ? await feedback.PlanAsync(id, asked.Plan)
            : await feedback.SetStatusAsync(id, status, asked.Reason, operatorName);
        if (changed.IsFailure)
        {
            return Refused(changed.Error);
        }

        var (kept, muted) = (changed.Value.Feedback, changed.Value.Muted);
        if (changed.Value.Changed)
        {
            await NoteStatusAsync(id, kept, operatorName);
        }

        if (muted is not null)
        {
            Console.WriteLine($"  Feedback from the sender of {id} muted until {muted.Until:u}: {FeedbackMute.SpamMarks} marked as spam in {FeedbackMute.SpamWindow.TotalDays:0} days.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackMute, id, $"{FeedbackMute.SpamMarks} marked as spam in {FeedbackMute.SpamWindow.TotalDays:0} days");
        }

        return await SheetAsync(id);
    }

    private async Task NoteStatusAsync(string id, Feedback kept, string operatorName)
    {
        Console.WriteLine($"  Feedback {id} marked {Name(kept.Status)}{(kept.Reason is null ? string.Empty : $" ({kept.Reason})")} by {operatorName}.");
        if (kept.IsSpam)
        {
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackSpam, id);
        }
        else
        {
            var why = kept.Status == FeedbackStatus.Planned ? kept.Plan : kept.Reason;
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackStatus, id, why is null ? Name(kept.Status) : $"{Name(kept.Status)}: {why}");
        }
    }

    private async Task<StudioResponse> KindAsync(string id, string body, string operatorName)
    {
        if (KindOf(Read<KindBody>(body)?.Kind) is not { } kind)
        {
            return Refused(FeedbackErrors.Invalid("Name a kind: bug or idea."));
        }

        var changed = await feedback.ChangeKindAsync(id, kind);
        if (changed.IsFailure)
        {
            return Refused(changed.Error);
        }

        if (changed.Value.Was is { } was)
        {
            Console.WriteLine($"  Feedback {id} turned into {(kind == FeedbackKind.Bug ? "a bug report" : "an idea")} by {operatorName}.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackConvert, id, $"{Name(was)} → {Name(kind)}");
        }

        return await SheetAsync(id);
    }

    private async Task<StudioResponse> MergeAsync(string id, string body, string operatorName)
    {
        if (Read<MergeBody>(body)?.Into is not { Length: > 0 } into)
        {
            return Refused(FeedbackErrors.Invalid("Name the idea to merge this one into."));
        }

        var merged = await feedback.MergeAsync(id, into);
        if (merged.IsFailure)
        {
            return Refused(merged.Error);
        }

        if (merged.Value.Changed)
        {
            Console.WriteLine($"  Idea {id} merged into {merged.Value.Into.Id} by {operatorName}.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackMerge, id, $"into {merged.Value.Into.Id}");
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

        if (muted.Value.Changed)
        {
            Console.WriteLine($"  Feedback from the sender of {id} muted until {muted.Value.Mute!.Until:u} by {operatorName}.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackMute, id, $"{FeedbackMute.Lasts.TotalDays:0} days");
        }

        return await SheetAsync(id);
    }

    private async Task<StudioResponse> UnmuteAsync(string id, string operatorName)
    {
        var lifted = await feedback.UnmuteAsync(id);
        if (lifted.IsFailure)
        {
            return Refused(lifted.Error);
        }

        if (lifted.Value.Changed)
        {
            Console.WriteLine($"  Feedback from the sender of {id} let through again by {operatorName}.");
            await Notes.NoteAsync(operatorName, OperatorActions.FeedbackUnmute, id);
        }

        return await SheetAsync(id);
    }

    /// <summary>
    /// What the sheet offers on this feedback as it stands, by the names the page knows its buttons by: marks and moves
    /// while it is open, an idea's plan, shipping, merge and plan prompt, Diagnose… while its table is played, and the
    /// mute or its lifting while a sender can hold one. An idea merged into another is changed through the one it joined:
    /// it offers only its dismissal, as spam or not, which takes it out of the group, and its sender's mute.
    /// </summary>
    private string[] Actions(Feedback one, FeedbackMute? mute, bool underWay, FeedbackGroup group)
    {
        var offered = new List<string>();
        if (one.MergedInto is not null)
        {
            offered.AddRange(["dismiss", "spam"]);
        }
        else
        {
            offered.AddRange(one.Kind == FeedbackKind.Bug ? BugActions(one) : IdeaActions(one, group));
            if (underWay && one.Context?.Table is not null)
            {
                offered.Add("diagnose");
            }
        }

        if (feedback.CanMute(one))
        {
            offered.Add(mute is null ? "mute" : "unmute");
        }

        return [.. offered];
    }

    private static IEnumerable<string> BugActions(Feedback one)
    {
        if (one.Status != FeedbackStatus.Seen)
        {
            yield return "seen";
        }

        if (one.Status != FeedbackStatus.Reproduced)
        {
            yield return "reproduced";
        }

        if (!one.IsClosed)
        {
            yield return "dismiss";
            yield return "spam";
            yield return "toIdea";
        }
    }

    private static IEnumerable<string> IdeaActions(Feedback one, FeedbackGroup group)
    {
        if (one.Status != FeedbackStatus.Seen)
        {
            yield return "seen";
        }

        // Planned again names another plan, so it is offered while planned too.
        yield return "planned";
        if (one.Status != FeedbackStatus.Shipped)
        {
            yield return "shipped";
        }

        if (!one.IsClosed)
        {
            yield return "dismiss";
            yield return "spam";
            yield return "declined";
            if (group.Merged.Count == 0)
            {
                yield return "toBug";
            }

            yield return "merge";
        }

        yield return "prompt";
    }

    private static object[] Additions(Feedback one) =>
        [.. one.Additions.Select(addition => new { at = addition.At, text = addition.Text, expected = addition.Expected })];

    private static FeedbackKind? KindOf(string? named) => named switch { "bug" => FeedbackKind.Bug, "idea" => FeedbackKind.Idea, _ => null };

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
        "Feedback.Closed" or "Feedback.NoSender" or "Feedback.MergedAway" or "Feedback.Grouped" => 409,
        "Feedback.Unavailable" => 503,
        _ => 400,
    });

    private static StudioResponse Error(string code, string message, int status) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status: status);

    private sealed record StatusBody(string? Status, string? Reason, string? Plan);

    private sealed record MergeBody(string? Into);

    private sealed record KindBody(string? Kind);
}
