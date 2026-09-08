namespace DownfallArena.Application.Agents;

/// <summary>
/// Which agent to seat: a kind, and for kinds that read a file (weights, a policy), its path. The text form,
/// <c>random</c> or <c>kind:path</c>, is what the command line takes and what run stamps carry.
/// </summary>
public sealed record AgentSpec(AgentKind Kind, string? Path = null)
{
    public static AgentSpec Random { get; } = new(AgentKind.Random);

    public static AgentSpec Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var separator = text.IndexOf(':', StringComparison.Ordinal);
        var kindText = separator < 0 ? text : text[..separator];
        var path = separator < 0 ? null : text[(separator + 1)..];
        if (!Enum.TryParse<AgentKind>(kindText, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new ArgumentException($"Unknown agent kind '{kindText}'. Known kinds: {string.Join(", ", Enum.GetNames<AgentKind>())}.", nameof(text));
        }

        return new AgentSpec(kind, string.IsNullOrWhiteSpace(path) ? null : path);
    }

    public override string ToString() => Path is null ? Kind.ToString() : $"{Kind}:{Path}";
}
