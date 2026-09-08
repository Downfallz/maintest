using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// What a batch plays: the rule set, both rosters and agents, how many matches, and the base seed. Match
/// <c>i</c> uses seed <c>BaseSeed + i</c>, so any single match of a batch can be replayed on its own.
/// </summary>
public sealed record SimulationScenario
{
    public required RuleSet RuleSet { get; init; }

    public required IReadOnlyList<CreatureDefinitionId> Player1Roster { get; init; }

    public required IReadOnlyList<CreatureDefinitionId> Player2Roster { get; init; }

    public AgentKind Player1Agent { get; init; } = AgentKind.Random;

    public AgentKind Player2Agent { get; init; } = AgentKind.Random;

    public required int Matches { get; init; }

    public int BaseSeed { get; init; }

    public int SeedOf(int index) => unchecked(BaseSeed + index);
}
