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

    public static readonly DomainError HostFull = new("Feedback.HostFull", "The game is taking no more feedback today. Try again tomorrow.");
}
