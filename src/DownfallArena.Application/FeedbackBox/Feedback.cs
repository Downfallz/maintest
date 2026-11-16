using System.Globalization;
using System.Security.Cryptography;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// A bug report or an idea a player sent with **Report or suggest** (ADR 0189, docs/tabletop/feedback-plan.md): their
/// words, what the page and the host added, and where it stands. <see cref="Sender" /> is who the spam guards count
/// against: the account when signed in, else a hash of the seat's key, never the key. <see cref="Account" /> is
/// dropped when the account is deleted; the text stays.
/// </summary>
public sealed record Feedback
{
    /// <summary>How long a closed piece of feedback is kept before the tending pass lets it go (ADR 0189).</summary>
    public static readonly TimeSpan KeptClosed = TimeSpan.FromDays(180);

    private const string AtFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    public required string Id { get; init; }

    public required FeedbackKind Kind { get; init; }

    public FeedbackStatus Status { get; init; } = FeedbackStatus.New;

    public required DateTimeOffset At { get; init; }

    /// <summary>When it was closed: fixed, shipped, declined or dismissed. None while open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    public required string Sender { get; init; }

    /// <summary>The account that sent it (<c>acct_…</c>), or none for a guest or a deleted account.</summary>
    public string? Account { get; init; }

    /// <summary>For a bug report its category; for an idea where it is about.</summary>
    public required string Category { get; init; }

    /// <summary>For a bug report what happened; for an idea what the player was trying to do and what got in the way.</summary>
    public required string Text { get; init; }

    /// <summary>For a bug report what the player expected; for an idea how they would like it. Optional.</summary>
    public string? Expected { get; init; }

    /// <summary>The page's address, without its query.</summary>
    public string? Page { get; init; }

    /// <summary>The browser's user agent.</summary>
    public string? Browser { get; init; }

    public IReadOnlyList<PageError> Errors { get; init; } = [];

    public FeedbackContext? Context { get; init; }

    public IReadOnlyList<FeedbackAddition> Additions { get; init; } = [];

    /// <summary>Whether the operator closed it: fixed, shipped, declined or dismissed.</summary>
    public bool IsClosed => Status is FeedbackStatus.Fixed or FeedbackStatus.Shipped or FeedbackStatus.Declined or FeedbackStatus.Dismissed;

    /// <summary>When its sender last wrote it: its sending, or the last text added to it.</summary>
    public DateTimeOffset LastWritten => Additions.Count == 0 ? At : Additions.Max(addition => addition.At);

    /// <summary>An id for feedback sent then: its time first, so ids sort as feedback came, then a few random characters.</summary>
    public static string NewId(DateTimeOffset at) =>
        $"{at.UtcDateTime.ToString(AtFormat, CultureInfo.InvariantCulture)}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";

    /// <summary>Whether an id is one feedback could have: what a store checks before it names a file or a blob after it.</summary>
    public static bool IsId(string? id) => SentAt(id) is not null;

    /// <summary>When the feedback an id names was sent, read off the id alone; none for an id no feedback could have.</summary>
    public static DateTimeOffset? SentAt(string? id) =>
        id is { Length: 26 }
        && id[19] == '-'
        && id[20..].All(char.IsAsciiHexDigitLower)
        && DateTimeOffset.TryParseExact(id[..19], AtFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : null;
}
