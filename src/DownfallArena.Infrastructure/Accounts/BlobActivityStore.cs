using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>Each account's activity as one blob of the accounts' container (ADR 0174), beside the account itself.</summary>
public sealed class BlobActivityStore : IAccountActivityStore
{
    private readonly BlobContainerClient _container;

    private BlobActivityStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobActivityStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobActivityStore(container);
    }

    public Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default) =>
        ReadAsync(ActivityDocuments.PathOf(account), cancellationToken);

    public async Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var kept = new List<AccountActivity>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, ActivityDocuments.Prefix, cancellationToken))
        {
            // A document forgotten between the listing and its read is not listed.
            if (await ReadAsync(item.Name, cancellationToken) is { } activity)
            {
                kept.Add(activity);
            }
        }

        return kept;
    }

    public async Task SaveAsync(AccountActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        await _container.GetBlobClient(ActivityDocuments.PathOf(activity.Account)).UploadAsync(
            BinaryData.FromString(ActivityDocuments.Write(activity)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(ActivityDocuments.PathOf(account), cancellationToken: cancellationToken);

    private async Task<AccountActivity?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _container.GetBlobClient(path).DownloadContentAsync(cancellationToken);
            return ActivityDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}
