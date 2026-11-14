using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;

namespace DownfallArena.Infrastructure.Admin;

/// <summary>
/// The operators' actions (admin plan, A6) as blobs of their own container, beside the recordings' one: a store of
/// sessions reads every top-level prefix of its container as a session. A listing returns names in order, so the newest
/// are found without a read.
/// </summary>
public sealed class BlobOperatorLog : IOperatorLog
{
    private readonly BlobContainerClient _container;

    private BlobOperatorLog(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobOperatorLog> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobOperatorLog(container);
    }

    public async Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _container.GetBlobClient(OperatorLogDocuments.NameOf(action.Id)).UploadAsync(
            BinaryData.FromString(OperatorLogDocuments.Write(action)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
        foreach (var gone in (await IdsAsync(cancellationToken)).Skip(OperatorAction.Kept))
        {
            await _container.DeleteBlobIfExistsAsync(OperatorLogDocuments.NameOf(gone), cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken))
        {
            if (OperatorLogDocuments.IdOf(item.Name) is { } id)
            {
                ids.Add(id);
            }
        }

        return [.. ids.OrderDescending(StringComparer.Ordinal)];
    }

    public async Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!OperatorAction.IsId(id))
        {
            return null;
        }

        try
        {
            var download = await _container.GetBlobClient(OperatorLogDocuments.NameOf(id)).DownloadContentAsync(cancellationToken);
            return OperatorLogDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}
