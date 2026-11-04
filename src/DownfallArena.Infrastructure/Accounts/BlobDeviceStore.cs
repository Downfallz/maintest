using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>An account's devices as one blob of the accounts' container (ADR 0122), beside the account itself.</summary>
public sealed class BlobDeviceStore : IDeviceStore
{
    private readonly BlobContainerClient _container;

    private BlobDeviceStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobDeviceStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobDeviceStore(container);
    }

    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(DeviceDocuments.PathOf(account)).DownloadContentAsync(cancellationToken);
            return DeviceDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return [];
        }
    }

    public async Task SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0)
        {
            await ForgetAsync(account, cancellationToken);
            return;
        }

        await _container.GetBlobClient(DeviceDocuments.PathOf(account)).UploadAsync(
            BinaryData.FromString(DeviceDocuments.Write(devices)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(DeviceDocuments.PathOf(account), cancellationToken: cancellationToken);
}
