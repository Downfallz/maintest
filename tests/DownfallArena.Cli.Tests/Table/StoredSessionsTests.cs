using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The runs of the tables as the operator's routes reach them: here, how one is deleted.</summary>
public sealed class StoredSessionsTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-stored-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// The record goes before the rest of the run (#818, #851): a side file's first write checks it, so nothing new is
    /// written once it is gone. The store's recursive delete is made to fail, and the record must be gone already.
    /// </summary>
    [Fact]
    public async Task Deleting_a_session_deletes_its_record_before_the_rest_of_its_run()
    {
        var store = new FileArtifactStore(_root, _ => throw new IOException("The directory could not be removed."));
        await store.Writer("run-o").WriteJsonAsync(TableRecord.File, new { Revision = 1 }, Cancel);
        await store.Writer("run-o").WriteJsonAsync(PlaytestRun.ReactionsFile, new { Items = 1 }, Cancel);

        await Should.ThrowAsync<IOException>(() => new StoredSessions(store).DeleteAsync("run-o", Cancel));

        File.Exists(Path.Combine(_root, "run-o", TableRecord.File)).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "run-o", PlaytestRun.ReactionsFile)).ShouldBeTrue();
    }
}
