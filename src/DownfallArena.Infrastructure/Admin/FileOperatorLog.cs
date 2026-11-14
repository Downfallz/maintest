using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;

namespace DownfallArena.Infrastructure.Admin;

/// <summary>
/// The operators' actions (admin plan, A6) as files of one directory, for a laptop's table. A record is written beside
/// its place and moved onto it, so a reader never sees half of one.
/// </summary>
public sealed class FileOperatorLog : IOperatorLog
{
    public FileOperatorLog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public async Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, OperatorLogDocuments.NameOf(action.Id));
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, OperatorLogDocuments.Write(action), cancellationToken);
        File.Move(staged, path, overwrite: true);
        foreach (var gone in (await IdsAsync(cancellationToken)).Skip(OperatorAction.Kept))
        {
            File.Delete(Path.Combine(Directory, OperatorLogDocuments.NameOf(gone)));
        }
    }

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = [.. System.IO.Directory.EnumerateFiles(Directory)
            .Select(OperatorLogDocuments.IdOf)
            .OfType<string>()
            .OrderDescending(StringComparer.Ordinal)];
        return Task.FromResult(ids);
    }

    public async Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!OperatorAction.IsId(id))
        {
            return null;
        }

        try
        {
            return OperatorLogDocuments.Read(await File.ReadAllTextAsync(Path.Combine(Directory, OperatorLogDocuments.NameOf(id)), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
