namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// How a run and a relative path become one blob name. The fence the file adapters keep with the filesystem
/// (a path may not leave its run) is kept here by hand, because a container has no directories to leave.
/// </summary>
internal static class BlobArtifactNames
{
    /// <summary>A run's prefix, one segment ending in a slash.</summary>
    public static string Prefix(string run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        var name = run.TrimEnd('/');
        if (name.Length == 0 || name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || name == "." || name == "..")
        {
            throw new ArgumentException($"'{run}' is not a run name: a run is one segment of a blob name.", nameof(run));
        }

        return name + "/";
    }

    /// <summary>The blob name of a relative path under a run prefix, refused when it would leave the run.</summary>
    public static string Resolve(string prefix, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var path = relativePath.Replace('\\', '/');
        var segments = path.Split('/');
        if (path.StartsWith('/') || segments.Any(segment => segment.Length == 0 || segment == "." || segment == ".."))
        {
            throw new ArgumentException($"Artifact path '{relativePath}' leaves the run.", nameof(relativePath));
        }

        return prefix + path;
    }
}
