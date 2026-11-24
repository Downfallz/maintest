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
    private readonly TextsRead<AccountId> _read = new();

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
        _read.Remember(account, text, version);
        return new DeviceListRead(text is null ? [] : DeviceDocuments.Read(text), version);
    }

    /// <summary>
    /// Writes back the fields this revision does not know (#1211), from the text read at that version: kept from this
    /// store's read, or read again (#1232). A write over a version that has moved since is refused: without being
    /// tried when the document was read again, by its condition when the text was kept.
    /// </summary>
    public async Task<bool> SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0)
        {
            _read.Forget(account);
        }

        string? before = null;
        if (version is not null && devices.Count > 0)
        {
            (var current, before) = await _read.AtAsync(account, version, token => ConditionalBlob.ReadAsync(Blob(account), token), cancellationToken);
            if (!current)
            {
                return false;
            }
        }

        var written = await ConditionalBlob.WriteAsync(Blob(account), devices.Count == 0 ? null : DeviceDocuments.Write(devices, before), version, cancellationToken);
        if (devices.Count == 0)
        {
            _read.Forget(account);
        }

        return written;
    }

    public async Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        _read.Forget(account);
        await _container.DeleteBlobIfExistsAsync(DeviceDocuments.PathOf(account), cancellationToken: cancellationToken);
        _read.Forget(account);
    }

    private BlobClient Blob(AccountId account) => _container.GetBlobClient(DeviceDocuments.PathOf(account));
}
