using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>An account's friendships as one blob of the accounts' container (ADR 0129), beside the account itself.</summary>
public sealed class BlobFriendStore : IFriendStore
{
    private readonly BlobContainerClient _container;

    private BlobFriendStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobFriendStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobFriendStore(container);
    }

    public async Task<FriendList> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(FriendDocuments.PathOf(account)).DownloadContentAsync(cancellationToken);
            return FriendDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return FriendList.Empty;
        }
    }

    public async Task SaveAsync(AccountId account, FriendList list, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (FriendDocuments.IsEmpty(list))
        {
            await ForgetAsync(account, cancellationToken);
            return;
        }

        await _container.GetBlobClient(FriendDocuments.PathOf(account)).UploadAsync(
            BinaryData.FromString(FriendDocuments.Write(list)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(FriendDocuments.PathOf(account), cancellationToken: cancellationToken);
}
