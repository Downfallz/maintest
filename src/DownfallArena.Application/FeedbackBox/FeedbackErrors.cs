using System.Globalization;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>Why feedback was not kept: stable codes the sheet turns into the sentence a player reads (ADR 0189).</summary>
public static class FeedbackErrors
{
    public static DomainError Invalid(string message) => new("Feedback.Invalid", message);

    public static DomainError TooSoon(TimeSpan left) =>
        new("Feedback.TooSoon", $"Wait {Math.Ceiling(left.TotalSeconds).ToString(CultureInfo.InvariantCulture)} seconds before sending more feedback.");

    public static readonly DomainError TooMany = new("Feedback.TooMany", "Five pieces of feedback a day is the most one player can send. Try again tomorrow.");

    public static readonly DomainError Muted = new("Feedback.Muted", "Feedback from you is paused for now.");

    public static DomainError NotFound(string id) => new("Feedback.NotFound", $"No feedback '{id}' at this host.");

    public static readonly DomainError Closed = new("Feedback.Closed", "This feedback is closed: mark it seen to reopen it first.");

    public static readonly DomainError MergedAway = new("Feedback.MergedAway", "This idea was merged into another one: change that one instead.");

    public static readonly DomainError Grouped = new("Feedback.Grouped", "Other ideas were merged into this one, so it stays an idea.");

    public static readonly DomainError NotABug = new("Feedback.NotABug", "Only a bug report is reproduced: turn this idea into a bug report first.");

    public static readonly DomainError NoPosition = new("Feedback.NoPosition", "This report does not say the round and seat it was sent from, at a table or in a practice: there is nothing to cut.");

    /// <summary>A redacted scenario that still names the session, keeps a time, or holds a person: it is not offered (R5).</summary>
    public static DomainError NotRedacted(string message) => new("Feedback.NotRedacted", message);

    public static readonly DomainError NotReproduced = new("Feedback.NotReproduced", "This report's round was not reproduced, or its scenario was removed since: reproduce it first.");

    public static readonly DomainError UnderWay = new("Feedback.UnderWay", "The game this report was sent from is still being played: its scenario is offered once it is over, so nothing a seat holds face down reaches a public issue.");

    public static readonly DomainError TableUnread = new("Feedback.TableUnread", "The record of the game this report was sent from cannot be read: nothing shows the game is over, so its scenario is not offered.");

    public static readonly DomainError NotFiled = new("Feedback.NotFiled", "Only a bug report filed as an issue is marked fixed, by its issue.");

    public static readonly DomainError NoSender = new("Feedback.NoSender", "Nobody can be muted for this feedback: its sender's account was deleted, or this host keeps no account activity.");

    public static readonly DomainError Unavailable = new("Feedback.Unavailable", "The feedback could not be read just now. Try again in a few minutes.");

    public static readonly DomainError HostFull = new("Feedback.HostFull", "The game is taking no more feedback today. Try again tomorrow.");
}
