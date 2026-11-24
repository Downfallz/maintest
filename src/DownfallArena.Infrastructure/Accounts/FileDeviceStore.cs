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

    /// <summary>
    /// The devices, for a push or a check that saves nothing: what they read is not kept, so a worker that sends to an
    /// account holds none of its keys past the send (ADR 0203).
    /// </summary>
    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, _) = await ConditionalFile.ReadAsync(FullPath(account), cancellationToken);
        return text is null ? [] : DeviceDocuments.Read(text);
    }

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
        return await _read.WriteAsync(
            account,
            version,
            devices.Count == 0 ? null : before => DeviceDocuments.Write(devices, before),
            token => ConditionalFile.ReadAsync(FullPath(account), token),
            (text, token) => ConditionalFile.WriteAsync(FullPath(account), text, version, token),
            cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        _read.ForgetAsync(account, () => ConditionalFile.DeleteAsync(FullPath(account), cancellationToken));

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, DeviceDocuments.PathOf(account));
}
