using System.Text;
using System.Text.Json;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// The journal as bytes: one JSON line a decision, in the artifacts' dialect. What a file and a blob share,
/// so the two adapters differ only in how they make an append conditional.
/// </summary>
internal static class JournalLines
{
    /// <summary>The journal's file under its run, the name the table has always written it under.</summary>
    public const string FileName = "decisions.jsonl";

    /// <summary>The decisions in a journal's bytes, and where it ends: its length is the mark an append is conditional on.</summary>
    public static JournalReading Read(ReadOnlySpan<byte> content)
    {
        var decisions = new List<SeatDecision>();
        var lines = 0;
        foreach (var text in Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            lines++;
            var line = JsonSerializer.Deserialize<JournalLine>(text, ArtifactJson.LineOptions)
                ?? throw new InvalidDataException($"Journal line {lines} is empty.");
            if (line.ToDecision() is { } decision)
            {
                decisions.Add(decision);
            }
        }

        return new JournalReading(decisions, new JournalPosition(lines, content.Length));
    }

    /// <summary>The lines to write after a position: numbered on from it, stamped with the time they were taken.</summary>
    public static byte[] Write(JournalPosition after, IReadOnlyList<SeatDecision> decisions, DateTimeOffset at)
    {
        using var stream = new MemoryStream();
        for (var index = 0; index < decisions.Count; index++)
        {
            stream.Write(JsonSerializer.SerializeToUtf8Bytes(JournalLine.Of(after.Lines + index + 1, at, decisions[index]), ArtifactJson.LineOptions));
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }
}
