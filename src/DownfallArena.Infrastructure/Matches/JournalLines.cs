using System.Text;
using System.Text.Json;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// The journal as bytes: one JSON line an entry, in the artifacts' dialect. What a file and a blob share,
/// so the two adapters differ only in how they make an append conditional.
/// </summary>
internal static class JournalLines
{
    /// <summary>The journal's file under its run, the name the table has always written it under.</summary>
    public const string FileName = "decisions.jsonl";

    /// <summary>The entries in a journal's bytes, and where it ends: its length is the mark an append is conditional on.</summary>
    public static JournalReading Read(ReadOnlySpan<byte> content)
    {
        var entries = new List<IJournalEntry>();
        var lines = 0;
        foreach (var text in Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            lines++;
            var line = JsonSerializer.Deserialize<JournalLine>(text, ArtifactJson.LineOptions)
                ?? throw new InvalidDataException($"Journal line {lines} is empty.");
            if (line.ToEntry() is { } entry)
            {
                entries.Add(entry);
            }
        }

        return new JournalReading(entries, new JournalPosition(lines, content.Length));
    }

    /// <summary>The lines to write after a position: numbered on from it, stamped with the time they were taken.</summary>
    public static byte[] Write(JournalPosition after, IReadOnlyList<IJournalEntry> entries, DateTimeOffset at)
    {
        using var stream = new MemoryStream();
        for (var index = 0; index < entries.Count; index++)
        {
            stream.Write(JsonSerializer.SerializeToUtf8Bytes(JournalLine.Of(after.Lines + index + 1, at, entries[index]), ArtifactJson.LineOptions));
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }
}
