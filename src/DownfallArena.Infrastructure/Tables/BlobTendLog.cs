using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// Tending runs as blobs of their own container, beside the recordings' one: a store of sessions reads every top-level
/// prefix of its container as a session. A listing returns names in order, so the newest are found without a read.
/// </summary>
public sealed class BlobTendLog : ITendLog
{
    private readonly BlobContainerClient _container;

    private BlobTendLog(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobTendLog> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobTendLog(container);
    }

    public async Task RecordAsync(TendRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _container.GetBlobClient(TendDocuments.NameOf(run.Id)).UploadAsync(
            BinaryData.FromString(TendDocuments.Write(run)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
        foreach (var gone in (await IdsAsync(cancellationToken)).Skip(TendRun.Kept))
        {
            await _container.DeleteBlobIfExistsAsync(TendDocuments.NameOf(gone), cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken))
        {
            if (TendDocuments.IdOf(item.Name) is { } id)
            {
                ids.Add(id);
            }
        }

        return [.. ids.OrderDescending(StringComparer.Ordinal)];
    }

    public async Task<TendRun?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!TendRun.IsId(id))
        {
            return null;
        }

        try
        {
            var download = await _container.GetBlobClient(TendDocuments.NameOf(id)).DownloadContentAsync(cancellationToken);
            return TendDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}
