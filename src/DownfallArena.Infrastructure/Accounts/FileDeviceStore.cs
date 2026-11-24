using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's devices as one file under the accounts' directory, for a laptop's table. Written beside its place
/// and moved onto it, so a reader never sees half a list, and only over the version read (ADR 0201); an empty list
/// is no file.
/// </summary>
public sealed class FileDeviceStore : IDeviceStore
{
    private readonly TextsRead<AccountId> _read = new();

    public FileDeviceStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
        (await ReadAsync(account, cancellationToken)).Devices;

    public async Task<DeviceListRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalFile.ReadAsync(FullPath(account), cancellationToken);
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
            (var current, before) = await _read.AtAsync(account, version, token => ConditionalFile.ReadAsync(FullPath(account), token), cancellationToken);
            if (!current)
            {
                return false;
            }
        }

        return await ConditionalFile.WriteAsync(FullPath(account), devices.Count == 0 ? null : DeviceDocuments.Write(devices, before), version, cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        _read.Forget(account);
        return ConditionalFile.DeleteAsync(FullPath(account), cancellationToken);
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, DeviceDocuments.PathOf(account));
}
