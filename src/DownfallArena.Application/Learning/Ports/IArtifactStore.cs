namespace DownfallArena.Application.Learning.Ports;

/// <summary>
/// Where runs are kept: a directory of run directories today, a blob container of run prefixes on Azure
/// (ADR 0080). A run is named by whoever opens it, and its writer and reader address the same artifacts.
/// Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IArtifactStore
{
    /// <summary>A writer for the run of that name; the run is created by the first write.</summary>
    IArtifactWriter Writer(string run);

    /// <summary>A reader for the run of that name; a run nobody wrote reads as empty.</summary>
    IArtifactReader Reader(string run);

    /// <summary>Where that run is, said to a person: a path on this machine or a URL.</summary>
    string LocationOf(string run);

    /// <summary>The name of every run the store holds, in ordinal order; empty when nothing was ever written.</summary>
    Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a run and everything written under it, the file named <paramref name="first" /> before anything else:
    /// a writer that checks that file before writing beside it then sees the run as gone before any of its files
    /// is listed (#818, #851). Every caller says which file that is, none for a run nothing checks, so a table's run
    /// is never deleted without its record first. False when there was no such run.
    /// </summary>
    Task<bool> DeleteAsync(string run, string? first, CancellationToken cancellationToken = default);
}
