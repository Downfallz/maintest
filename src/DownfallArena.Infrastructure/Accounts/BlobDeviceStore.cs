using Azure.Storage.Blobs;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's devices as one blob of the accounts' container (ADR 0122), beside the account itself, written only
/// over the tag it was read at (ADR 0201).
/// </summary>
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

    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
        (await ReadAsync(account, cancellationToken)).Devices;

    public async Task<DeviceListRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalBlob.ReadAsync(Blob(account), cancellationToken);
        return new DeviceListRead(text is null ? [] : DeviceDocuments.Read(text), version);
    }

    /// <summary>
    /// Reads the document again to write back what this revision does not know of it (#1211): a write over a version
    /// that has moved since is refused here, without being tried.
    /// </summary>
    public async Task<bool> SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);
        string? before = null;
        if (version is not null && devices.Count > 0)
        {
            (before, var current) = await ConditionalBlob.ReadAsync(Blob(account), cancellationToken);
            if (!string.Equals(current, version, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return await ConditionalBlob.WriteAsync(Blob(account), devices.Count == 0 ? null : DeviceDocuments.Write(devices, before), version, cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _container.DeleteBlobIfExistsAsync(DeviceDocuments.PathOf(account), cancellationToken: cancellationToken);

    private BlobClient Blob(AccountId account) => _container.GetBlobClient(DeviceDocuments.PathOf(account));
}
