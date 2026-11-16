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

    public static readonly DomainError NoSender = new("Feedback.NoSender", "Nobody can be muted for this feedback: its sender's account was deleted, or this host keeps no account activity.");

    public static readonly DomainError Unavailable = new("Feedback.Unavailable", "The feedback could not be read just now. Try again in a few minutes.");

    public static readonly DomainError HostFull = new("Feedback.HostFull", "The game is taking no more feedback today. Try again tomorrow.");
}
