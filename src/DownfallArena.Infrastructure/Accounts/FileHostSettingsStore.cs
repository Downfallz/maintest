using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The host's settings as one file under the accounts' directory (ADR 0177), for a laptop's table. Written beside its
/// place and moved onto it, so a reader never sees half a document.
/// </summary>
public sealed class FileHostSettingsStore : IHostSettingsStore
{
    public FileHostSettingsStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = System.IO.Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    private string FullPath => System.IO.Path.Combine(RootDirectory, HostSettingsDocuments.Path);

    public async Task<HostSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return HostSettingsDocuments.Read(await File.ReadAllTextAsync(FullPath, cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task SaveAsync(HostSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FullPath)!);
        var staged = $"{FullPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, HostSettingsDocuments.Write(settings), cancellationToken);
        File.Move(staged, FullPath, overwrite: true);
    }
}
