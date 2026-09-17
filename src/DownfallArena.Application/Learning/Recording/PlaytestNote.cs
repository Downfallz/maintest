using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// One line of <c>notes.jsonl</c>: what a playtest knows that neither the trace nor the dataset carries
/// (<c>docs/tabletop/playtest-app.md</c>, 5.3) — how long a decision took, a refusal a person provoked, a rule
/// someone had to look up, and a misplay they own up to.
/// </summary>
/// <remarks>
/// It carries no identifier of its own and none is added to <see cref="StepRecord" />: the alignment with
/// <c>steps.jsonl</c> is by order, because both are appended in the order that seat decided. That holds for a
/// seat a person played throughout, and only for such a seat: a <see cref="NoteKind.Decision" /> note is
/// written when a person's tap is accepted, while a step is recorded for whoever was seated, so a bot seat and
/// the rounds before a handover are steps with no note beside them. A reader joining the two files has to know
/// which seat a person held, which is what the session's run stamp says.
/// </remarks>
public sealed record PlaytestNote
{
    /// <summary>
    /// The session this note belongs to, which is the name of its run directory. A plain string rather than a
    /// typed identifier: a session is a directory the host named, not something the domain has an identity for.
    /// </summary>
    public required string SessionId { get; init; }

    public required MatchId MatchId { get; init; }

    public required PlayerSlot Slot { get; init; }

    public int? Round { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    /// <summary>When, in UTC, from the injected <see cref="TimeProvider" /> and never from the wall clock.</summary>
    public required DateTimeOffset At { get; init; }

    public required NoteKind Kind { get; init; }

    /// <summary>
    /// For a <see cref="NoteKind.Decision" />: from the moment this seat's options were served to the moment
    /// the decision was accepted. It measures a person reading a screen, so it is the served moment that
    /// starts it and not the moment the engine asked — the two differ by however long nobody was looking.
    /// </summary>
    public long? ElapsedMs { get; init; }

    /// <summary>For a <see cref="NoteKind.Refused" />: the code of the <see cref="DomainError" /> returned.</summary>
    public string? Code { get; init; }

    /// <summary>For a <see cref="NoteKind.Refused" />: the message of that error.</summary>
    public string? Message { get; init; }

    /// <summary>For a <see cref="NoteKind.Lookup" />, a <see cref="NoteKind.Misplay" /> or a <see cref="NoteKind.Comment" />: what the player typed or picked.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// A decision that was accepted, timed from when its options were served. The elapsed time is read off the
    /// same <paramref name="timeProvider" /> as <see cref="At" />, so a test that moves the clock moves both
    /// and a session recorded under a frozen clock reports zero rather than a wall-clock accident.
    /// </summary>
    public static PlaytestNote Decision(
        string sessionId,
        MatchId matchId,
        PlayerSlot slot,
        int? round,
        RoundSubPhase? subPhase,
        DateTimeOffset servedAt,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var at = timeProvider.GetUtcNow();
        return new PlaytestNote
        {
            SessionId = sessionId,
            MatchId = matchId,
            Slot = slot,
            Round = round,
            SubPhase = subPhase,
            At = at,
            Kind = NoteKind.Decision,
            // Never negative: a served moment in the future is a clock that moved, not a decision taken before
            // it was asked, and a negative duration in a dataset is worse than a zero.
            ElapsedMs = (long)Math.Max(0, (at - servedAt).TotalMilliseconds),
        };
    }

    /// <summary>A decision that was refused, with the error whoever refused it gave.</summary>
    public static PlaytestNote Refused(
        string sessionId,
        MatchId matchId,
        PlayerSlot slot,
        int? round,
        RoundSubPhase? subPhase,
        DomainError error,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new PlaytestNote
        {
            SessionId = sessionId,
            MatchId = matchId,
            Slot = slot,
            Round = round,
            SubPhase = subPhase,
            At = timeProvider.GetUtcNow(),
            Kind = NoteKind.Refused,
            Code = error.Code,
            Message = error.Message,
        };
    }

    /// <summary>
    /// A note a player produced: a lookup, a misplay or a comment. The kind is checked here rather than
    /// trusted, because the three that a person can write are exactly the three the host must not invent.
    /// </summary>
    public static PlaytestNote Typed(
        string sessionId,
        MatchId matchId,
        PlayerSlot slot,
        int? round,
        RoundSubPhase? subPhase,
        NoteKind kind,
        string text,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (kind is not (NoteKind.Lookup or NoteKind.Misplay or NoteKind.Comment))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{kind} is written by the host, not typed by a player.");
        }

        return new PlaytestNote
        {
            SessionId = sessionId,
            MatchId = matchId,
            Slot = slot,
            Round = round,
            SubPhase = subPhase,
            At = timeProvider.GetUtcNow(),
            Kind = kind,
            Text = text,
        };
    }
}
