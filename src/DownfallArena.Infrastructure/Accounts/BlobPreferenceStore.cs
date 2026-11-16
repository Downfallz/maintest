using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>An account's game settings as one blob of the accounts' container (ADR 0140), beside the account itself.</summary>
public sealed class BlobPreferenceStore : IPreferenceStore
{
    private readonly BlobContainerClient _container;

    private BlobPreferenceStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobPreferenceStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobPreferenceStore(container);
    }

    public async Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(PreferenceDocuments.PathOf(account)).DownloadContentAsync(cancellationToken);
            return PreferenceDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await _container.GetBlobClient(PreferenceDocuments.PathOf(account)).UploadAsync(
            BinaryData.FromString(PreferenceDocuments.Write(preferences)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(PreferenceDocuments.PathOf(account), cancellationToken: cancellationToken);
}
