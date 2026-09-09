namespace DownfallArena.Cli.Studio;

/// <summary>The whole alias map, as the page wants it written.</summary>
internal sealed record SaveAliasesRequest
{
    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
