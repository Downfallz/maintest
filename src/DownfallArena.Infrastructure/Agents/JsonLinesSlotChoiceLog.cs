using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Agents;

/// <summary>
/// A slot choice log as a file of JSON lines, one slot a line, in the learning artifacts' dialect (ADR 0013).
/// An evaluation plays its matches side by side, so a line is written whole under a lock and flushed at once:
/// a run killed part way through leaves every slot it read up to then.
/// </summary>
public sealed class JsonLinesSlotChoiceLog : ISlotChoiceLog, IDisposable
{
    private readonly Lock _gate = new();
    private readonly StreamWriter _writer;

    public JsonLinesSlotChoiceLog(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
    }

    public void Record(SlotChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var line = JsonSerializer.Serialize(choice, ArtifactJson.LineOptions);
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
