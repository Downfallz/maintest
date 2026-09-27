using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Where one run lives in its store: its name, where that is as its players are told it, and the writer and
/// reader over it. One thing rather than four, because they are asked of the store together and mean nothing
/// apart.
/// </summary>
internal sealed record RunPlace(string SessionId, string Location, IArtifactWriter Writer, IArtifactReader Reader)
{
    public static RunPlace In(IArtifactStore store, string id)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new RunPlace(id, store.LocationOf(id), store.Writer(id), store.Reader(id));
    }
}
