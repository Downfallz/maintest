using System.Reflection;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The git commit the engine was built from, and whether the tree had local changes, read back from the
/// informational version the build stamps into the assemblies (ADR 0013).
/// </summary>
public sealed record EngineVersion(string Commit, bool IsDirty)
{
    public const string Unknown = "unknown";

    private const string DirtySuffix = "-dirty";

    public static EngineVersion Current { get; } =
        Parse(typeof(EngineVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public bool IsKnown => Commit != Unknown;

    /// <summary>
    /// Reads the "+commit[-dirty]" part of an informational version such as "1.0.0+abc123def456-dirty".
    /// </summary>
    public static EngineVersion Parse(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        if (informationalVersion is null || plus < 0 || plus == informationalVersion.Length - 1)
        {
            return new EngineVersion(Unknown, false);
        }

        var revision = informationalVersion[(plus + 1)..];
        var dirty = revision.EndsWith(DirtySuffix, StringComparison.Ordinal);
        var commit = dirty ? revision[..^DirtySuffix.Length] : revision;
        return commit.Length == 0 ? new EngineVersion(Unknown, dirty) : new EngineVersion(commit, dirty);
    }

    public override string ToString() => IsDirty ? Commit + DirtySuffix : Commit;
}
