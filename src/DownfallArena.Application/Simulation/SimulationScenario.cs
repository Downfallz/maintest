using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// What a batch plays: the rule set, both rosters and agents, how many matches, and the seeds. Match <c>i</c>
/// uses seed <c>BaseSeed + i</c>, or <c>Seeds[i]</c> when an explicit seed list is given (the benchmark seeds),
/// so any single match of a batch can be replayed on its own.
/// </summary>
public sealed record SimulationScenario
{
    public required RuleSet RuleSet { get; init; }

    public required IReadOnlyList<CreatureDefinitionId> Player1Roster { get; init; }

    public required IReadOnlyList<CreatureDefinitionId> Player2Roster { get; init; }

    public AgentSpec Player1Agent { get; init; } = AgentSpec.Random;

    public AgentSpec Player2Agent { get; init; } = AgentSpec.Random;

    public required int Matches { get; init; }

    public int BaseSeed { get; init; }

    /// <summary>An explicit seed per match, taking precedence over <see cref="BaseSeed"/>; must hold at least <see cref="Matches"/> seeds.</summary>
    public IReadOnlyList<int>? Seeds { get; init; }

    public int SeedOf(int index) => Seeds is { } seeds ? seeds[index] : unchecked(BaseSeed + index);
}
