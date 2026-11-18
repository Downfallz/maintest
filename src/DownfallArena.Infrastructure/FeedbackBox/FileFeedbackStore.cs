using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Infrastructure.FeedbackBox;

/// <summary>
/// Feedback (ADR 0189) as files of one directory, for a laptop's table. A record is written beside its place and moved
/// onto it, so a reader never sees half of one.
/// </summary>
public sealed class FileFeedbackStore : IFeedbackStore, IFeedbackIndexStore
{
    /// <summary>The file a write of the index holds alone while it checks the version and moves its copy in.</summary>
    private const string IndexLock = "index.lock";

    /// <summary>How long a write of the index waits for another process's, a short try at a time.</summary>
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);

    public FileFeedbackStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public async Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (!Feedback.IsId(feedback.Id))
        {
            throw new ArgumentException($"'{feedback.Id}' is not a feedback id.", nameof(feedback));
        }

        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, FeedbackDocuments.NameOf(feedback.Id));
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, FeedbackDocuments.Write(feedback), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = [.. System.IO.Directory.EnumerateFiles(Directory)
            .Select(FeedbackDocuments.IdOf)
            .OfType<string>()
            .OrderDescending(StringComparer.Ordinal)];
        return Task.FromResult(ids);
    }

    public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = [.. FeedbackDocuments.DaysOf(after, until)
            .SelectMany(day => System.IO.Directory.EnumerateFiles(Directory, $"{day}*{FeedbackDocuments.Extension}"))
            .Select(FeedbackDocuments.IdOf)
            .OfType<string>()
            .Where(id => FeedbackDocuments.SentWithin(id, after, until))
            .OrderDescending(StringComparer.Ordinal)];
        return Task.FromResult(ids);
    }

    public async Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Feedback.IsId(id))
        {
            return null;
        }

        try
        {
            return FeedbackDocuments.Read(await File.ReadAllTextAsync(Path.Combine(Directory, FeedbackDocuments.NameOf(id)), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or System.Text.Json.JsonException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>The index of feedback (#866), its version a hash of what the file holds.</summary>
    public async Task<FeedbackIndexRead> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        var text = await IndexTextAsync(cancellationToken);
        return text is null ? new FeedbackIndexRead(null, null) : new FeedbackIndexRead(FeedbackDocuments.ReadIndex(text), VersionOf(text));
    }

    public async Task<bool> WriteIndexAsync(FeedbackIndexContent content, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        System.IO.Directory.CreateDirectory(Directory);

        // A lock file held alone, so a table and a `table-tend` run on the same directory never interleave a check and a move.
        await using var held = await LockedAsync(cancellationToken);
        var now = await IndexTextAsync(cancellationToken);
        if ((now is null ? null : VersionOf(now)) != version)
        {
            return false;
        }

        var path = Path.Combine(Directory, FeedbackDocuments.IndexName);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, FeedbackDocuments.WriteIndex(content), cancellationToken);
        File.Move(staged, path, overwrite: true);
        return true;
    }

    /// <summary>
    /// The lock file, held alone; it is left in place, since a file deleted while another process waits on it would let
    /// two writers each hold a lock of their own.
    /// </summary>
    private async Task<FileStream> LockedAsync(CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + LockWait;
        while (true)
        {
            try
            {
                return new FileStream(Path.Combine(Directory, IndexLock), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }
    }

    private async Task<string?> IndexTextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(Directory, FeedbackDocuments.IndexName), cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string VersionOf(string text) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Feedback.IsId(id))
        {
            File.Delete(Path.Combine(Directory, FeedbackDocuments.NameOf(id)));
        }

        return Task.CompletedTask;
    }
}
