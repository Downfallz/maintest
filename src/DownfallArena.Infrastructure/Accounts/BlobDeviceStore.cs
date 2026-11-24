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

    /// <summary>
    /// The devices, for a push or a check that saves nothing: what they read is not kept, so a worker that sends to an
    /// account holds none of its keys past the send (ADR 0203).
    /// </summary>
    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, _) = await ConditionalBlob.ReadAsync(Blob(account), cancellationToken);
        return text is null ? [] : DeviceDocuments.Read(text);
    }

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
        return await _read.WriteAsync(
            account,
            version,
            devices.Count == 0 ? null : before => DeviceDocuments.Write(devices, before),
            token => ConditionalBlob.ReadAsync(Blob(account), token),
            (text, token) => ConditionalBlob.WriteAsync(Blob(account), text, version, token),
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _read.ForgetAsync(account, () => _container.DeleteBlobIfExistsAsync(DeviceDocuments.PathOf(account), cancellationToken: cancellationToken));

    private BlobClient Blob(AccountId account) => _container.GetBlobClient(DeviceDocuments.PathOf(account));
}
