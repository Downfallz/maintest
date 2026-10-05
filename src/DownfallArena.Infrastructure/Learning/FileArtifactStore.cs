using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Runs as directories under one root: <c>runs/playtest/&lt;id&gt;/</c> is this store over <c>runs/playtest</c>.
/// </summary>
public sealed class FileArtifactStore : IArtifactStore
{
    public FileArtifactStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public IArtifactWriter Writer(string run) => new FileArtifactWriter(LocationOf(run));

    public IArtifactReader Reader(string run) => new FileArtifactReader(LocationOf(run));

    public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> runs = Directory.Exists(RootDirectory)
            ? [.. Directory.GetDirectories(RootDirectory).Select(Path.GetFileName).Select(name => name!).Order(StringComparer.Ordinal)]
            : [];
        return Task.FromResult(runs);
    }

    public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default)
    {
        var directory = LocationOf(run);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult(false);
        }

        Directory.Delete(directory, recursive: true);
        return Task.FromResult(true);
    }

    public string LocationOf(string run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        if (run.Contains('/', StringComparison.Ordinal) || run.Contains('\\', StringComparison.Ordinal) || run == "." || run == "..")
        {
            throw new ArgumentException($"'{run}' is not a run name: a run is one directory under the root.", nameof(run));
        }

        // The checks above already make `run` a bare name, so GetFileName returns it unchanged; it is here so a
        // path built from a request reads as one name under the root to the analysis that follows it (S2083).
        return Path.Combine(RootDirectory, Path.GetFileName(run));
    }
}
