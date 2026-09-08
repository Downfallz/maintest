namespace DownfallArena.Application.Agents;

/// <summary>
/// Which agent to seat: a kind, for kinds that read a file its path, and once resolved a version that
/// fingerprints what the file held, so two runs on different weights at the same path stamp differently.
/// The text form, <c>random</c>, <c>kind:path</c>, or <c>kind:path@version</c>, is what the command line
/// takes and what run stamps carry.
/// </summary>
public sealed record AgentSpec(AgentKind Kind, string? Path = null, string? Version = null)
{
    public static AgentSpec Random { get; } = new(AgentKind.Random);

    public static AgentSpec Greedy { get; } = new(AgentKind.Greedy);

    public static AgentSpec Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var separator = text.IndexOf(':', StringComparison.Ordinal);
        var kindText = separator < 0 ? text : text[..separator];
        var rest = separator < 0 ? string.Empty : text[(separator + 1)..];
        if (!Enum.TryParse<AgentKind>(kindText, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new ArgumentException($"Unknown agent kind '{kindText}'. Known kinds: {string.Join(", ", Enum.GetNames<AgentKind>())}.", nameof(text));
        }

        var at = rest.LastIndexOf('@');
        var path = at < 0 ? rest : rest[..at];
        var version = at < 0 ? null : rest[(at + 1)..];
        return new AgentSpec(kind, string.IsNullOrWhiteSpace(path) ? null : path, string.IsNullOrWhiteSpace(version) ? null : version);
    }

    public override string ToString()
    {
        if (Path is null)
        {
            return Kind.ToString();
        }

        return Version is null ? $"{Kind}:{Path}" : $"{Kind}:{Path}@{Version}";
    }
}
