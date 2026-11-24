namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// How long a test of the table host waits for something the host does on its own time (a question asked, a push
/// sent) before it says it never happened. A deadline in time, not a count of looks: a count is a few seconds, which
/// a loaded runner can spend before the host has moved (#1187).
/// </summary>
internal static class Waits
{
    public static readonly TimeSpan Within = TimeSpan.FromMinutes(2);
}
