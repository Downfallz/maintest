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
}
