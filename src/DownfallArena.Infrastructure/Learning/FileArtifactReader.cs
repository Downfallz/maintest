using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Reads the files a <see cref="FileArtifactWriter" /> wrote under one root directory. The same fence as the
/// writer: a relative path that escapes the root is refused.
/// </summary>
public sealed class FileArtifactReader : IArtifactReader
{
    public FileArtifactReader(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public Task<IReadOnlyList<string>> ListAsync(string relativePrefix, CancellationToken cancellationToken = default)
    {
        var directory = Resolve(relativePrefix);
        IReadOnlyList<string> paths = Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).Select(name => $"{relativePrefix}/{name}")]
            : [];
        return Task.FromResult(paths);
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    private string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        if (Path.IsPathRooted(relativePath) || !fullPath.StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Artifact path '{relativePath}' leaves the run directory.", nameof(relativePath));
        }

        return fullPath;
    }
}
