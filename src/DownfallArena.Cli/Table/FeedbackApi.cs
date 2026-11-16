using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Learning;
using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;

namespace DownfallArena.Cli.Table;

/// <summary>
/// <c>POST /api/feedback</c> (ADR 0189, docs/tabletop/feedback-plan.md, R1): a bug report or an idea, from the seat whose
/// key the request carries or from a signed-in account. The page sends the words; the host adds what it knows and the
/// page cannot be trusted with -- the table, the seat, where it stands, the content, rules and engine, and, when the
/// player attaches it, the seat's own view, or a practice's entries since its cut. The guards against spam are
/// <see cref="SendFeedback" />'s.
/// </summary>
internal sealed class FeedbackApi(SendFeedback desk, TableRegistry registry)
{
    public const string Route = "/api/feedback";

    /// <summary>The players' sign-in, or none on a host where nobody signs in: only seats send feedback there.</summary>
    public AccountApi? Accounts { get; init; }

    /// <summary>The scenarios being practised in this process, whose seats send feedback too.</summary>
    public ScenarioPractices? Practices { get; init; }

    public static bool Names(string path) => path == Route;

    /// <param name="token">The seat's key the request carries, if any.</param>
    /// <param name="address">The network address the request came from, counted for a guest and never kept.</param>
    /// <param name="browser">The browser's user agent.</param>
    public async Task<StudioResponse> HandleAsync(string method, string body, string? token, AccountRequest account, string? address, string? browser)
    {
        if (method != "POST")
        {
            return StudioResponse.OfPlainText(404, $"No such route: {method} {Route}");
        }

        FeedbackBody? posted;
        try
        {
            posted = JsonSerializer.Deserialize<FeedbackBody>(body, ArtifactJson.LineOptions);
        }
        catch (JsonException)
        {
            posted = null;
        }

        if (posted?.Kind is not ("bug" or "idea"))
        {
            return Problem(400, "Feedback.Invalid", "Feedback is a bug report (\"kind\":\"bug\") or an idea (\"kind\":\"idea\").");
        }

        var signedIn = Accounts is null ? null : (await Accounts.WhoAsync(account))?.Account.Id.ToString();
        var from = await FromAsync(token, signedIn, posted.Kind == "bug" && posted.Attach);
        if (from.Refusal is { } refusal)
        {
            return refusal;
        }

        if (signedIn is null && from.Context is null)
        {
            return Problem(401, "Feedback.SignInRequired", "Sign in, or send it from a game you are playing.");
        }

        var bug = posted.Kind == "bug";
        var sent = new FeedbackSent
        {
            Kind = bug ? FeedbackKind.Bug : FeedbackKind.Idea,
            Sender = signedIn ?? SeatSender(token!),
            Account = signedIn,
            Address = address,
            Category = bug ? posted.Category : posted.Where,
            Text = bug ? posted.Happened : posted.Problem,
            Expected = bug ? posted.Expected : posted.Wish,
            Page = PageOf(posted.Page),
            Browser = browser,
            Errors = bug ? [.. (posted.Errors ?? []).Select(error => new PageError(error.Message ?? string.Empty, error.Where))] : [],
            Context = bug ? from.Context : from.Context is { } context ? new FeedbackContext { Table = context.Table, Scenario = context.Scenario, Round = context.Round } : null,
        };

        var answer = await desk.HandleAsync(sent);
        if (answer.Refused is { } refused)
        {
            var retry = answer.RetryAfter is { } wait ? (long)Math.Ceiling(wait.TotalSeconds) : (long?)null;
            var status = refused.Code == "Feedback.Invalid" ? 400 : 429;
            var response = StudioResponse.OfJson(new { error = refused.Code, message = refused.Message, retryAfterSeconds = retry }, ArtifactJson.LineOptions, status: status);
            return retry is { } seconds
                ? response with { Headers = [new KeyValuePair<string, string>("Retry-After", seconds.ToString(CultureInfo.InvariantCulture))] }
                : response;
        }

        return answer.Merged
            ? StudioResponse.OfJson(new { id = answer.Id, merged = true, code = "Feedback.Merged" }, ArtifactJson.LineOptions)
            : StudioResponse.OfJson(new { id = answer.Id, merged = false }, ArtifactJson.LineOptions, status: 201);
    }

    /// <summary>What the seat the token names adds, or why the token may not send for it; nothing for a request without a seat.</summary>
    private async Task<(FeedbackContext? Context, StudioResponse? Refusal)> FromAsync(string? token, string? signedIn, bool attach)
    {
        if (string.IsNullOrEmpty(token))
        {
            return (null, null);
        }

        // A practice is found by its seat's token in this process, and is its account's alone.
        if (Practices?.ByToken(token) is { } practice)
        {
            if (practice.Account != signedIn)
            {
                return (null, Problem(403, "Feedback.NotYourSeat", "This practice is another account's."));
            }

            var moment = await practice.Play.Api.MomentAsync(token, attach);
            return (new FeedbackContext
            {
                Scenario = practice.Scenario,
                Seat = moment?.Seat.Name,
                Round = moment?.Round,
                SubPhase = moment?.SubPhase,
                JournalLength = moment?.JournalLength,
                Rules = RuleSetStamp.Of(practice.Play.Session.Rules),
                Engine = EngineVersion.Current.ToString(),
                Position = moment?.Position,
                PracticeEntries = attach ? ScenarioFile.JournalLinesOf(await practice.Play.SinceTheCutAsync()) : null,
            }, null);
        }

        if (await registry.ByTokenAsync(token) is not { } table || await table.Api.MomentAsync(token, attach) is not { } seated)
        {
            // A key that names no seat names nobody: the request is as if it carried none.
            return (null, null);
        }

        // On a host where players sign in a seat is its account's (ADR 0122): its key alone does not speak for it.
        if (seated.Seat.Account is { } owner && owner.ToString() != signedIn)
        {
            return (null, signedIn is null
                ? Problem(401, "Feedback.SignInRequired", "Sign in to send feedback from this seat.")
                : Problem(403, "Feedback.NotYourSeat", "This seat belongs to another player."));
        }

        return (new FeedbackContext
        {
            Table = table.Id,
            Seat = seated.Seat.Name,
            Round = seated.Round,
            SubPhase = seated.SubPhase,
            JournalLength = seated.JournalLength,
            Content = table.Content,
            Rules = RuleSetStamp.Of(table.Session.Rules),
            Engine = EngineVersion.Current.ToString(),
            Position = seated.Position,
        }, null);
    }

    /// <summary>A guest is counted against a hash of its seat's key: the key itself is a secret and is never kept.</summary>
    private static string SeatSender(string token) => $"seat-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..16]}";

    /// <summary>The page's address without its query, which can carry a seat's key.</summary>
    private static string? PageOf(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return null;
        }

        var cut = page.IndexOfAny(['?', '#']);
        return cut < 0 ? page : page[..cut];
    }

    private static StudioResponse Problem(int status, string error, string message) =>
        StudioResponse.OfJson(new { error, message }, ArtifactJson.LineOptions, status: status);
}
