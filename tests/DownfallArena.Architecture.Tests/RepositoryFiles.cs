namespace DownfallArena.Architecture.Tests;

/// <summary>Where the repository is on disk, for the tests that read its files instead of its assemblies.</summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string PathTo(params string[] segments) => Path.Combine([Root, .. segments]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DownfallArena.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test binaries.");
    }
}
