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
        if (!File.Exists(path))
        {
            return null;
        }

        // Shared with a writer: a table's journal and notes are appended to while the store lists and reads
        // them, and a reader that asked for the file to itself would refuse that writer, or be refused by it.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task<TaggedText?> ReadTaggedAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            bytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        return new TaggedText(System.Text.Encoding.UTF8.GetString(bytes), FileTags.Of(bytes));
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
