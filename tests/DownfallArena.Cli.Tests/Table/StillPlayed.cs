using System.Text.Json;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A recorded table written back as still played: what a scenario cut before #736 from a table still under way sees
/// of its source, now that the host refuses such a cut (#833).
/// </summary>
internal static class StillPlayed
{
    public static async Task ReopenAsync(string runsDirectory, string session)
    {
        var path = Path.Combine(runsDirectory, session, TableRecord.File);
        var record = StoredSessions.RecordOf(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(record with { Status = TableRecord.Open, Void = false }, ArtifactJson.DocumentOptions), TestContext.Current.CancellationToken);
    }
}
