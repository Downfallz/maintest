using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// Plays a scenario's matches one after the other through the public commands and collects their results.
/// Every random roll comes from the seeds, so a batch replays identically.
/// </summary>
public sealed class BatchRunner(
    ICommandHandler<CreateMatch, Result<MatchId>> createMatch,
    ICommandHandler<JoinMatch, Result<PlayerSlot>> joinMatch,
    IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>> boardState,
    MatchDriver driver,
    IRandomSourceFactory random)
{
    public async Task<BatchResult> RunAsync(SimulationScenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenario.Matches);

        var results = new List<MatchResult>(scenario.Matches);
        for (var index = 0; index < scenario.Matches; index++)
        {
            results.Add(await PlayOneAsync(scenario, index, cancellationToken));
        }

        return new BatchResult(scenario, results, SimulationSummary.Of(results));
    }

    private async Task<MatchResult> PlayOneAsync(SimulationScenario scenario, int index, CancellationToken cancellationToken)
    {
        var seed = scenario.SeedOf(index);
        var matchId = Accept(await createMatch.HandleAsync(new CreateMatch(scenario.RuleSet, seed), cancellationToken));
        Accept(await joinMatch.HandleAsync(new JoinMatch(matchId, PlayerId.New(), scenario.Player1Roster), cancellationToken));
        Accept(await joinMatch.HandleAsync(new JoinMatch(matchId, PlayerId.New(), scenario.Player2Roster), cancellationToken));

        var player1 = Agent(scenario.Player1Agent, unchecked((seed * 31) + 1));
        var player2 = Agent(scenario.Player2Agent, unchecked((seed * 31) + 2));
        var outcome = Accept(await driver.PlayAsync(matchId, player1, player2, cancellationToken));

        var board = Accept(await boardState.HandleAsync(new GetBoardStateForPlayer(matchId, PlayerSlot.Player1), cancellationToken));
        return new MatchResult(
            index,
            seed,
            matchId,
            outcome,
            board.RoundNumber ?? 0,
            board.Allies.Sum(creature => creature.Health.Value),
            board.Enemies.Sum(creature => creature.Health.Value));
    }

    private IPlayerAgent Agent(AgentKind kind, int seed) =>
        kind switch
        {
            AgentKind.Random => new RandomAgent(random.Create(seed)),
            _ => throw new InvalidOperationException($"Agent kind '{kind}' has no implementation."),
        };

    private static TValue Accept<TValue>(Result<TValue> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"A simulation step was refused: {result.Error.Code} ({result.Error.Message}).");
}
