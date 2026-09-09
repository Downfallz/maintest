namespace DownfallArena.Application.Agents;

/// <summary>
/// The kinds of player agent the engine can seat. New agents get a value here and a case in <see cref="AgentFactory"/>.
/// </summary>
public enum AgentKind
{
    /// <summary>Picks uniformly among the options.</summary>
    Random,

    /// <summary>One-step lookahead with the built-in weights; the deterministic baseline.</summary>
    Greedy,

    /// <summary>One-step lookahead with weights read from the file the spec names.</summary>
    Heuristic,

    /// <summary>A trained policy read from the <c>policy.json</c> the spec names (docs/learning/training.md).</summary>
    Policy,

    /// <summary>Greedy, but a share of decisions the spec names are taken at random, to record exploration (ADR 0014).</summary>
    Explore,
}
