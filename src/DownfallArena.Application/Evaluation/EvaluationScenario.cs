using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// What an evaluation plays: two agents on one roster, each seed twice with the agents swapped.
/// </summary>
public sealed record EvaluationScenario
{
    public required RuleSet RuleSet { get; init; }

    public required IReadOnlyList<CreatureDefinitionId> Roster { get; init; }

    public required AgentSpec AgentA { get; init; }

    public required AgentSpec AgentB { get; init; }

    public required IReadOnlyList<int> Seeds { get; init; }
}
