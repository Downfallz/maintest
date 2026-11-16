using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Infrastructure.FeedbackBox;

/// <summary>
/// Feedback (ADR 0189) as files of one directory, for a laptop's table. A record is written beside its place and moved
/// onto it, so a reader never sees half of one.
/// </summary>
public sealed class FileFeedbackStore : IFeedbackStore
{
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
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Feedback.IsId(id))
        {
            File.Delete(Path.Combine(Directory, FeedbackDocuments.NameOf(id)));
        }

        return Task.CompletedTask;
    }
}
