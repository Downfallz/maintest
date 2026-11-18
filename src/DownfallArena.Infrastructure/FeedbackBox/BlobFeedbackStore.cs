using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Infrastructure.FeedbackBox;

/// <summary>
/// Feedback (ADR 0189) as blobs of its own container, beside the recordings' one: a store of sessions reads every
/// top-level prefix of its container as a session. A listing returns names in order, so the newest are found without a
/// read; names start with the day the feedback was sent, so the last days are listed by their prefixes alone.
/// </summary>
public sealed class BlobFeedbackStore : IFeedbackStore, IFeedbackIndexStore
{
    private readonly BlobContainerClient _container;

    private BlobFeedbackStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobFeedbackStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobFeedbackStore(container);
    }

    public async Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (!Feedback.IsId(feedback.Id))
        {
            throw new ArgumentException($"'{feedback.Id}' is not a feedback id.", nameof(feedback));
        }

        await _container.GetBlobClient(FeedbackDocuments.NameOf(feedback.Id)).UploadAsync(
            BinaryData.FromString(FeedbackDocuments.Write(feedback)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken))
        {
            if (FeedbackDocuments.IdOf(item.Name) is { } id)
            {
                ids.Add(id);
            }
        }

        return [.. ids.OrderDescending(StringComparer.Ordinal)];
    }

    public async Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        foreach (var day in FeedbackDocuments.DaysOf(after, until))
        {
            await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: day, cancellationToken))
            {
                if (FeedbackDocuments.IdOf(item.Name) is { } id && FeedbackDocuments.SentWithin(id, after, until))
                {
                    ids.Add(id);
                }
            }
        }

        return [.. ids.OrderDescending(StringComparer.Ordinal)];
    }

    public async Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Feedback.IsId(id))
        {
            return null;
        }

        try
        {
            var download = await _container.GetBlobClient(FeedbackDocuments.NameOf(id)).DownloadContentAsync(cancellationToken);
            return FeedbackDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException)
        {
            // A record that cannot be read is skipped, never the route: one bad record must not refuse every sender.
            return null;
        }
    }

    /// <summary>The index of feedback (#866), its version the blob's tag.</summary>
    public async Task<FeedbackIndexRead> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(FeedbackDocuments.IndexName).DownloadContentAsync(cancellationToken);
            return new FeedbackIndexRead(FeedbackDocuments.ReadIndex(download.Value.Content.ToString()), download.Value.Details.ETag.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return new FeedbackIndexRead(null, null);
        }
    }

    /// <summary>Written over the tag read, or only where none is yet: another process's write since is never lost.</summary>
    public async Task<bool> WriteIndexAsync(FeedbackIndexContent content, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            await _container.GetBlobClient(FeedbackDocuments.IndexName).UploadAsync(
                BinaryData.FromString(FeedbackDocuments.WriteIndex(content)),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                    Conditions = version is null ? new BlobRequestConditions { IfNoneMatch = ETag.All } : new BlobRequestConditions { IfMatch = new ETag(version) },
                },
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Feedback.IsId(id))
        {
            await _container.DeleteBlobIfExistsAsync(FeedbackDocuments.NameOf(id), cancellationToken: cancellationToken);
        }
    }
}
