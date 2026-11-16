using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Keeps a bug report or an idea a player sent (ADR 0189), behind the guards against spam, checked in this order and
/// counted over both kinds (docs/tabletop/feedback-plan.md, "Against spam"): two minutes apart, five a day per sender,
/// five a day per network address for a guest, a second report on the same position (or a second idea within the
/// hour) merged into the first, and a ceiling for the whole host.
/// </summary>
public sealed class SendFeedback(IFeedbackStore store, GuestAddresses addresses, TimeProvider clock)
{
    public const int ShortestText = 10;

    public const int LongestText = 2000;

    public const int MostErrors = 10;

    public const int PerSender = 5;

    public const int PerAddress = 5;

    public const int PerHost = 100;

    public static readonly TimeSpan Apart = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan Day = TimeSpan.FromDays(1);

    /// <summary>How long a second idea from the same sender is merged into the first.</summary>
    public static readonly TimeSpan IdeaMerged = TimeSpan.FromHours(1);

    /// <summary>What a bug report names as its category: the rules, how it looks, stuck or slow, or something else.</summary>
    public static readonly IReadOnlyList<string> BugCategories = ["rules", "looks", "stuck", "other"];

    /// <summary>What an idea names as where it is about: the rules, the battle, the lobby and friends, or something else.</summary>
    public static readonly IReadOnlyList<string> IdeaPlaces = ["rules", "battle", "lobby", "other"];

    private const int LongestLine = 500;

    public async Task<FeedbackAnswer> HandleAsync(FeedbackSent sent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sent);
        if (Invalid(sent) is { } invalid)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.Invalid(invalid));
        }

        var now = clock.GetUtcNow();
        var recent = await RecentAsync(now, cancellationToken);
        var mine = recent.Where(one => one.Sender == sent.Sender).ToList();

        var last = mine.Count == 0 ? (DateTimeOffset?)null : mine.Max(one => one.LastWritten);
        if (last is { } then && now - then < Apart)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.TooSoon(Apart - (now - then)), Apart - (now - then));
        }

        if (mine.Count + mine.Sum(one => one.Additions.Count(addition => addition.At > now - Day)) >= PerSender)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.TooMany, Day);
        }

        var guest = sent.Account is null && sent.Address is not null;
        if (guest && addresses.SentSince(sent.Address!, now) >= PerAddress)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.TooMany, Day);
        }

        var text = sent.Text!.Trim();
        var expected = Trimmed(sent.Expected);
        if (Merging(sent, mine, now) is { } first)
        {
            await store.SaveAsync(first with { Additions = [.. first.Additions, new FeedbackAddition(now, text, expected)] }, cancellationToken);
            Counted(sent, guest, now);
            return FeedbackAnswer.MergedInto(first.Id);
        }

        if (recent.Count >= PerHost)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.HostFull, Day);
        }

        var feedback = new Feedback
        {
            Id = Feedback.NewId(now),
            Kind = sent.Kind,
            At = now,
            Sender = sent.Sender,
            Account = sent.Account,
            Category = sent.Category!,
            Text = text,
            Expected = expected,
            Page = Trimmed(sent.Page),
            Browser = Trimmed(sent.Browser),
            Errors = [.. sent.Errors.TakeLast(MostErrors).Select(error => new PageError(Line(error.Message), Trimmed(error.Where) is { } where ? Line(where) : null))],
            Context = sent.Context,
        };
        await store.SaveAsync(feedback, cancellationToken);
        Counted(sent, guest, now);
        return FeedbackAnswer.Kept(feedback.Id);
    }

    private void Counted(FeedbackSent sent, bool guest, DateTimeOffset now)
    {
        if (guest)
        {
            addresses.Note(sent.Address!, now);
        }
    }

    /// <summary>The feedback sent in the last day, read off the ids first so nothing older is opened.</summary>
    private async Task<List<Feedback>> RecentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recent = new List<Feedback>();
        foreach (var id in await store.IdsAsync(cancellationToken))
        {
            if (Feedback.SentAt(id) is not { } at || at <= now - Day)
            {
                break;
            }

            if (await store.FindAsync(id, cancellationToken) is { } found)
            {
                recent.Add(found);
            }
        }

        return recent;
    }

    /// <summary>The open feedback this is one more word on: a report on the same position, or an idea within the hour.</summary>
    private static Feedback? Merging(FeedbackSent sent, List<Feedback> mine, DateTimeOffset now) => sent.Kind switch
    {
        FeedbackKind.Bug when sent.Context?.Place is { } place =>
            mine.FirstOrDefault(one => one.Kind == FeedbackKind.Bug && !one.IsClosed && one.Context?.Place == place),
        FeedbackKind.Idea =>
            mine.FirstOrDefault(one => one.Kind == FeedbackKind.Idea && !one.IsClosed && now - one.At < IdeaMerged),
        _ => null,
    };

    private static string? Invalid(FeedbackSent sent)
    {
        var (categories, field) = sent.Kind == FeedbackKind.Bug ? (BugCategories, "category") : (IdeaPlaces, "where");
        if (sent.Category is null || !categories.Contains(sent.Category, StringComparer.Ordinal))
        {
            return $"The {field} is one of: {string.Join(", ", categories)}.";
        }

        var text = sent.Text?.Trim() ?? string.Empty;
        if (text.Length is < ShortestText or > LongestText)
        {
            return $"Write between {ShortestText} and {LongestText} characters.";
        }

        if ((sent.Expected?.Trim().Length ?? 0) > LongestText)
        {
            return $"The second answer holds {LongestText} characters at most.";
        }

        return (sent.Page?.Length ?? 0) > LongestLine || (sent.Browser?.Length ?? 0) > LongestLine
            ? $"The page and the browser hold {LongestLine} characters at most."
            : null;
    }

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Line(string? text) => text is null ? string.Empty : text.Length <= LongestLine ? text : text[..LongestLine];
}
