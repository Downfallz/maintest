namespace DownfallArena.Application.Simulation;

/// <summary>
/// Everything a batch produced: the scenario, one result per match, and the summary.
/// </summary>
public sealed record BatchResult(SimulationScenario Scenario, IReadOnlyList<MatchResult> Results, SimulationSummary Summary);
