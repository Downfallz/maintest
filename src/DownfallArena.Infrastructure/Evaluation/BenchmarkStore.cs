using System.Text.Json;
using DownfallArena.Application.Evaluation;

namespace DownfallArena.Infrastructure.Evaluation;

/// <summary>
/// The benchmark directory: the fixed seed list and one digest per content hash (ADR 0013, decisions G and I).
/// Plain JSON, read and written with the default conventions, since a digest holds only plain values.
/// </summary>
public sealed class BenchmarkStore
{
    public const string SeedsFile = "benchmark-seeds.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public BenchmarkStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public string SeedsPath => Path.Combine(Directory, SeedsFile);

    public string DigestPath(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return Path.Combine(Directory, contentHash + ".json");
    }

    public IReadOnlyList<int> LoadSeeds() => LoadSeeds(SeedsPath);

    /// <summary>Reads a seed file (<c>{"seeds": [...]}</c>) and refuses an empty list or a repeated seed.</summary>
    public static IReadOnlyList<int> LoadSeeds(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = JsonSerializer.Deserialize<SeedsFileContent>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"'{path}' does not hold a seed list.");
        if (file.Seeds is not { Count: > 0 } seeds)
        {
            throw new InvalidDataException($"'{path}' holds no seeds.");
        }

        if (seeds.Distinct().Count() != seeds.Count)
        {
            throw new InvalidDataException($"'{path}' repeats a seed.");
        }

        return seeds;
    }

    public BenchmarkDigest? TryLoadDigest(string contentHash)
    {
        var path = DigestPath(contentHash);
        return File.Exists(path) ? JsonSerializer.Deserialize<BenchmarkDigest>(File.ReadAllText(path), Options) : null;
    }

    public string WriteDigest(BenchmarkDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        System.IO.Directory.CreateDirectory(Directory);
        var path = DigestPath(digest.ContentHash);
        File.WriteAllText(path, Serialize(digest) + Environment.NewLine);
        return path;
    }

    public static string Serialize(BenchmarkDigest digest) => JsonSerializer.Serialize(digest, Options);

    private sealed record SeedsFileContent(IReadOnlyList<int>? Seeds);
}
