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
}
