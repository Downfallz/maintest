using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The sessions a host's store holds, live or not: what the admin panel lists, exports and deletes, and what
/// the session page falls back to for a table this process no longer has. A table lives in memory and goes
/// with the replica (ADR 0080); its run is what survives, and this is the one place that reads a run by its
/// name alone.
/// </summary>
internal sealed class StoredSessions(IArtifactStore store)
{
    /// <summary>The files a run is made of, in the order the viewer wants them: the trace first, then the dataset.</summary>
    private static readonly string[] Documents = [RunRecorder.ManifestFile, RunRecorder.EpisodesFile, RunRecorder.StepsFile, PlaytestRun.NotesFile, PlaytestRun.CatalogueFile];

    public string LocationOf(string id) => store.LocationOf(id);

    /// <summary>Every run in the store, newest first, as its manifest describes it. A run without a manifest is listed by its name alone.</summary>
    public async Task<IReadOnlyList<StoredSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        var sessions = new List<StoredSession>();
        foreach (var id in await store.RunsAsync(cancellationToken))
        {
            sessions.Add(await DescribeAsync(id, cancellationToken));
        }

        return [.. sessions.OrderByDescending(session => session.Id, StringComparer.Ordinal)];
    }

    public async Task<StoredSession> DescribeAsync(string id, CancellationToken cancellationToken = default)
    {
        var manifest = await ManifestAsync(id, cancellationToken);
        return new StoredSession(
            id,
            manifest?.CreatedAt,
            manifest?.Stamp.Player1Agent,
            manifest?.Stamp.Player2Agent,
            manifest?.Stamp.BaseSeed,
            manifest?.Matches ?? 0,
            manifest?.Steps ?? 0,
            store.LocationOf(id));
    }

    /// <summary>
    /// The session's files, as the viewer reads them: every trace, then the manifest, the episodes, the steps,
    /// the notes and the catalogue. Empty when nothing was written under that name.
    /// </summary>
    public Task<IReadOnlyList<(string Name, string Text)>> ArtifactsAsync(string id, CancellationToken cancellationToken = default) =>
        ArtifactsOf(store.Reader(id), cancellationToken);

    public static async Task<IReadOnlyList<(string Name, string Text)>> ArtifactsOf(IArtifactReader reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        List<(string Name, string Text)> artifacts = [];
        foreach (var trace in await reader.ListAsync(RunRecorder.TracesDirectory, cancellationToken))
        {
            if (trace.EndsWith(".json", StringComparison.Ordinal) && await reader.ReadTextAsync(trace, cancellationToken) is { } text)
            {
                artifacts.Add((trace, text));
            }
        }

        foreach (var name in Documents)
        {
            if (await reader.ReadTextAsync(name, cancellationToken) is { } text)
            {
                artifacts.Add((name, text));
            }
        }

        return artifacts;
    }

    /// <summary>
    /// The runs named, as one zip holding a directory per run with the run's files under it exactly as the
    /// store holds them. Unzipped under <c>runs/</c>, each is what <c>train-clone</c>, <c>export-csv</c> and
    /// the viewer read. Runs that hold nothing are left out, and named in the result.
    /// </summary>
    public async Task<(byte[] Zip, IReadOnlyList<string> Exported, IReadOnlyList<string> Missing)> ZipAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var exported = new List<string>();
        var missing = new List<string>();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                var artifacts = await ArtifactsAsync(id, cancellationToken);
                if (artifacts.Count == 0)
                {
                    missing.Add(id);
                    continue;
                }

                exported.Add(id);
                foreach (var (name, text) in artifacts)
                {
                    var entry = zip.CreateEntry($"{id}/{name}", CompressionLevel.Optimal);
                    await using var content = await entry.OpenAsync(cancellationToken);
                    await content.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
                }
            }
        }

        return (buffer.ToArray(), exported, missing);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => store.DeleteAsync(id, cancellationToken);

    /// <summary>The table's record, or none when the run was never a table of a host that wrote one (ADR 0091).</summary>
    public async Task<TableRecord?> RecordAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var text = await store.Reader(id).ReadTextAsync(TableRecord.File, cancellationToken);
            return text is null ? null : JsonSerializer.Deserialize<TableRecord>(text, ArtifactJson.DocumentOptions);
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Every decision the table took, in order; empty when none was written.</summary>
    public async Task<IReadOnlyList<JournalEntry>> JournalAsync(string id, CancellationToken cancellationToken = default)
    {
        var text = await store.Reader(id).ReadTextAsync(DecisionJournal.File, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var entries = new List<JournalEntry>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (JsonSerializer.Deserialize<JournalEntry>(line, ArtifactJson.LineOptions) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private async Task<RunManifest?> ManifestAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var text = await store.Reader(id).ReadTextAsync(RunRecorder.ManifestFile, cancellationToken);
            return text is null ? null : JsonSerializer.Deserialize<RunManifest>(text, ArtifactJson.DocumentOptions);
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException)
        {
            // A manifest from another schema, or a directory that is not a run at all: listed by name, so the
            // operator can still delete it.
            return null;
        }
    }
}

/// <summary>One run as the store describes it. Agents and seed are the stamp's; null when the run has no readable manifest.</summary>
internal sealed record StoredSession(string Id, DateTimeOffset? CreatedAt, string? Player1Agent, string? Player2Agent, int? Seed, int Matches, int Steps, string Location);
