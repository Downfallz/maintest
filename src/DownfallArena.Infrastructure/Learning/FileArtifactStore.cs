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

    public string LocationOf(string run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        if (run.Contains('/', StringComparison.Ordinal) || run.Contains('\\', StringComparison.Ordinal) || run == "." || run == "..")
        {
            throw new ArgumentException($"'{run}' is not a run name: a run is one directory under the root.", nameof(run));
        }

        return Path.Combine(RootDirectory, run);
    }
}
