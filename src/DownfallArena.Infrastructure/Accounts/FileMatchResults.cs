using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>Results as files under the accounts' directory, for a laptop's table (ADR 0122, stage 5).</summary>
public sealed class FileMatchResults : IMatchResults
{
    public FileMatchResults(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(RootDirectory, ResultDocuments.PathOf(result));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, ResultDocuments.Write(result), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public async Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(RootDirectory, ResultDocuments.Prefix);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var results = new List<MatchResult>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            results.Add(ResultDocuments.Read(await File.ReadAllTextAsync(file, cancellationToken)));
        }

        return [.. results.OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal)];
    }
}
