using System.Text.Json;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// The index of the tables as files under a directory (ADR 0161): one per join code under <c>codes/</c>, one per
/// open table under <c>open/</c>. What a host on a directory store keeps beside it, as one process's.
/// </summary>
/// <remarks>
/// One reader or writer at a time in this process, whichever index over the directory it comes through, which
/// is what makes a read and the write that depends on it one step. Two processes over one directory are not
/// kept apart, as with the journal and the record on disk: a directory store is one process's.
/// </remarks>
public sealed class FileTableIndex : ITableIndex
{
    private static readonly Lock Gate = new();

    private readonly string _codes;
    private readonly string _open;

    public FileTableIndex(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        _codes = Path.Combine(root, "codes");
        _open = Path.Combine(root, "open");
    }

    public Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var path = PathOf(_codes, code);
        lock (Gate)
        {
            return Task.FromResult(Read<IndexedCode>(path));
        }
    }

    public Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        var path = PathOf(_codes, code.Code);
        lock (Gate)
        {
            if (File.Exists(path))
            {
                return Task.FromResult(false);
            }

            Write(path, code);
            return Task.FromResult(true);
        }
    }

    public Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default)
    {
        var path = PathOf(_codes, code);
        lock (Gate)
        {
            if (Read<IndexedCode>(path) is { } held && held.Table == table)
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    public Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        Put(new Listed(table.Table, table.CreatedAt, table.Revision, table.Summary, Closed: false));
        return Task.CompletedTask;
    }

    public Task CloseAsync(string table, DateTimeOffset createdAt, long revision, CancellationToken cancellationToken = default)
    {
        Put(new Listed(table, createdAt, revision, Summary: string.Empty, Closed: true));
        return Task.CompletedTask;
    }

    /// <summary>A table's entry written, unless the one held is of the same revision of its record or a later one.</summary>
    private void Put(Listed listed)
    {
        var path = PathOf(_open, listed.Table);
        lock (Gate)
        {
            if (Read<Listed>(path) is not { } held || held.Revision < listed.Revision)
            {
                Write(path, listed);
            }
        }
    }

    public Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        lock (Gate)
        {
            IReadOnlyList<IndexedTable> open = Directory.Exists(_open)
                ? [.. Directory.EnumerateFiles(_open, "*.json")
                    .Select(Read<Listed>)
                    .OfType<Listed>()
                    .Where(table => !table.Closed)
                    .Select(table => new IndexedTable(table.Table, table.CreatedAt, table.Revision, table.Summary))
                    .OrderByDescending(table => table.CreatedAt)
                    .ThenByDescending(table => table.Table, StringComparer.Ordinal)
                    .Skip(Math.Max(0, skip))
                    .Take(Math.Max(0, take))]
                : [];
            return Task.FromResult(open);
        }
    }

    /// <summary>A file under a directory of the index, named by a key that cannot leave it.</summary>
    private static string PathOf(string directory, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            throw new ArgumentException($"'{key}' is not a key the table index holds.", nameof(key));
        }

        // The check above already keeps the key a bare name. The canonical path is still checked against the very
        // prefix it was built from, the shape the path traversal analysis (S2083) reads as a guard, as the
        // artifact writer does.
        var under = directory + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(under, Path.GetFileName($"{key}.json")));
        if (!path.StartsWith(under, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{key}' is not a key the table index holds.", nameof(key));
        }

        return path;
    }

    private static TEntry? Read<TEntry>(string path)
        where TEntry : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TEntry>(File.ReadAllBytes(path), ArtifactJson.DocumentOptions) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written beside and moved over, so a reader finds the entry before or after and never half of it.</summary>
    private static void Write<TEntry>(string path, TEntry entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var beside = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(beside, JsonSerializer.SerializeToUtf8Bytes(entry, ArtifactJson.DocumentOptions));
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>An open table's entry, or what is left of it once the table closed: its revision, kept so no earlier summary lists it again.</summary>
    private sealed record Listed(string Table, DateTimeOffset CreatedAt, long Revision, string Summary, bool Closed);
}
