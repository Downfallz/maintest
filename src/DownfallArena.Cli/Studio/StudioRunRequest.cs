namespace DownfallArena.Cli.Studio;

/// <summary>
/// The run panel's form: a mode, two agent specs, how many seeds, the seed to start from, and the scoring
/// weights when the panel is driving a heuristic agent from its own sliders rather than from a committed file.
/// </summary>
internal sealed record StudioRunRequest
{
    public string Mode { get; init; } = StudioRunModes.Match;

    public string Player1 { get; init; } = "random";

    public string Player2 { get; init; } = "random";

    public int Matches { get; init; } = 20;

    public int? Seed { get; init; }

    /// <summary>
    /// Scoring weights by name (<c>docs/learning/agents.md</c>), or <c>null</c>. When set they are written into
    /// the run's own directory and handed to every <c>heuristic</c> agent of this run that names no file, so a
    /// run made of sliders keeps the weights it was played with next to its result.
    /// </summary>
    public IReadOnlyDictionary<string, double>? Weights { get; init; }
}
