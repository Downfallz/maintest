using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// Tending runs as files of one directory, for a laptop's table. A record is written beside its place and moved onto
/// it, so a reader never sees half of one.
/// </summary>
public sealed class FileTendLog : ITendLog
{
    public FileTendLog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public async Task RecordAsync(TendRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, TendDocuments.NameOf(run.Id));
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, TendDocuments.Write(run), cancellationToken);
        File.Move(staged, path, overwrite: true);
        foreach (var gone in (await IdsAsync(cancellationToken)).Skip(TendRun.Kept))
        {
            File.Delete(Path.Combine(Directory, TendDocuments.NameOf(gone)));
        }
    }

    public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = [.. System.IO.Directory.EnumerateFiles(Directory)
            .Select(TendDocuments.IdOf)
            .OfType<string>()
            .OrderDescending(StringComparer.Ordinal)];
        return Task.FromResult(ids);
    }

    public async Task<TendRun?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!TendRun.IsId(id))
        {
            return null;
        }

        try
        {
            return TendDocuments.Read(await File.ReadAllTextAsync(Path.Combine(Directory, TendDocuments.NameOf(id)), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
