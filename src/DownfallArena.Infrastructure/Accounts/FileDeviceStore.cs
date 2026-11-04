using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's devices as one file under the accounts' directory, for a laptop's table. Written beside its place
/// and moved onto it, so a reader never sees half a list; an empty list is no file.
/// </summary>
public sealed class FileDeviceStore : IDeviceStore
{
    public FileDeviceStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            return DeviceDocuments.Read(await File.ReadAllTextAsync(FullPath(account), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
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

        var path = FullPath(account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, DeviceDocuments.Write(devices), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        File.Delete(FullPath(account));
        return Task.CompletedTask;
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, DeviceDocuments.PathOf(account));
}
