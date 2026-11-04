using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a replay of a recorded table depends on: the engine's commit, the content's hash and the host's rule set.
/// A record refused under one basis is refused again under the same one, so a host that finds it marked with its
/// own basis leaves it as it is rather than replaying it once more (ADR 0091).
/// </summary>
internal sealed record ReplayBasis(string Engine, string Content, RuleSetStamp Rules)
{
    /// <summary>
    /// This host's basis, or none when its engine cannot be told apart from another build: an unknown commit, or a
    /// tree with local changes, where a fix since the refusal would carry the same name and never be tried.
    /// </summary>
    public static ReplayBasis? Of(EngineVersion engine, IGameResources resources, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);
        return engine.IsKnown && !engine.IsDirty ? new ReplayBasis(engine.ToString(), resources.Version, RuleSetStamp.Of(rules)) : null;
    }
}
