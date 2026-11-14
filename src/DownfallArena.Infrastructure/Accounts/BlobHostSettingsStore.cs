using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>The host's settings as one blob of the accounts' container (ADR 0177), read by every process of the host.</summary>
public sealed class BlobHostSettingsStore : IHostSettingsStore
{
    private readonly BlobContainerClient _container;

    private BlobHostSettingsStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobHostSettingsStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobHostSettingsStore(container);
    }

    public async Task<HostSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(HostSettingsDocuments.Path).DownloadContentAsync(cancellationToken);
            return HostSettingsDocuments.Read(download.Value.Content.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAsync(HostSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _container.GetBlobClient(HostSettingsDocuments.Path).UploadAsync(
            BinaryData.FromString(HostSettingsDocuments.Write(settings)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }
}
