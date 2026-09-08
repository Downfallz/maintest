using System.Text.Json;
using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Writes artifacts as files under one root directory, creating directories as needed. Paths stay inside the
/// root: a relative path that escapes it is refused.
/// </summary>
public sealed class FileArtifactWriter : IArtifactWriter
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    public FileArtifactWriter(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var path = Resolve(relativePath);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, value, ArtifactJson.DocumentOptions, cancellationToken);
    }

    public async Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var path = Resolve(relativePath);
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        foreach (var value in values)
        {
            await stream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value, ArtifactJson.LineOptions), cancellationToken);
            await stream.WriteAsync(NewLine, cancellationToken);
        }
    }

    private string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        if (Path.IsPathRooted(relativePath) || !fullPath.StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Artifact path '{relativePath}' leaves the run directory.", nameof(relativePath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? RootDirectory);
        return fullPath;
    }
}
