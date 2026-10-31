using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

internal static class JoinCodeAnswers
{
    /// <summary>A code that names no open seat sends the page to say so (ADR 0116), and carries no token on the way.</summary>
    public static void ShouldNameNoSeat(this StudioResponse answer)
    {
        answer.Status.ShouldBe(303);
        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == JoinCodes.Spent);
    }
}
