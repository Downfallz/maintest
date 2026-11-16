using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Keeps a bug report or an idea a player sent (ADR 0189), behind the guards against spam, checked in this order and
/// counted over both kinds (docs/tabletop/feedback-plan.md, "Against spam"): two minutes apart, five a day per sender,
/// five a day per network address for a guest, a second report on the same position (or a second idea within the
/// hour) merged into the first, and a ceiling for the whole host. Merged text counts against the sender's five a day
/// as a sending does, though it adds no row. One write at a time (<see cref="FeedbackWrites" />).
/// </summary>
public sealed class SendFeedback(IFeedbackStore store, GuestAddresses addresses, FeedbackWrites writes, TimeProvider clock)
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

        return await writes.OneAtATimeAsync(() => GuardedAsync(sent, cancellationToken), cancellationToken);
    }

    private async Task<FeedbackAnswer> GuardedAsync(FeedbackSent sent, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var read = await RecentAsync(now, cancellationToken);
        var recent = read.Where(one => one.At > now - Day).ToList();

        // A sender's feedback is what they wrote in the day: sent in it, or sent the day before and added to in it.
        var mine = read.Where(one => one.Sender == sent.Sender && one.LastWritten > now - Day).ToList();

        // A page that lost the answer sends the same words again: it is told they were kept, not that it was too soon.
        if (mine.FirstOrDefault(one => one.Text == sent.Text!.Trim() || one.Additions.Any(addition => addition.Text == sent.Text!.Trim())) is { } repeated
            && now - repeated.LastWritten < Apart)
        {
            return FeedbackAnswer.MergedInto(repeated.Id);
        }

        var last = mine.Count == 0 ? (DateTimeOffset?)null : mine.Max(one => one.LastWritten);
        if (last is { } then && now - then < Apart)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.TooSoon(Apart - (now - then)), Apart - (now - then));
        }

        var written = mine.Where(one => one.At > now - Day).Select(one => one.At)
            .Concat(mine.SelectMany(one => one.Additions).Select(addition => addition.At).Where(at => at > now - Day))
            .Order()
            .ToList();
        if (written.Count >= PerSender)
        {
            return FeedbackAnswer.Refuse(FeedbackErrors.TooMany, written[^PerSender] + Day - now);
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
            return FeedbackAnswer.Refuse(FeedbackErrors.HostFull, recent.Min(one => one.At) + Day - now);
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
            Page = Trimmed(sent.Page) is { } page ? Line(page) : null,
            Browser = Trimmed(sent.Browser) is { } browser ? Line(browser) : null,
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

    /// <summary>
    /// The feedback sent in the last two days, read off the ids first so nothing older is opened: what was merged into
    /// the day before's still counts while its additions are within the day.
    /// </summary>
    private async Task<List<Feedback>> RecentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recent = new List<Feedback>();
        foreach (var id in await store.IdsAsync(cancellationToken))
        {
            if (Feedback.SentAt(id) is not { } at || at <= now - Day - Day)
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

        return null;
    }

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Line(string? text) => text is null ? string.Empty : text.Length <= LongestLine ? text : text[..LongestLine];
}
