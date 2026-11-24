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
            (before, var current) = await ConditionalFile.ReadAsync(FullPath(account), cancellationToken);
            if (!string.Equals(current, version, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return await ConditionalFile.WriteAsync(FullPath(account), devices.Count == 0 ? null : DeviceDocuments.Write(devices, before), version, cancellationToken);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        ConditionalFile.DeleteAsync(FullPath(account), cancellationToken);

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, DeviceDocuments.PathOf(account));
}
