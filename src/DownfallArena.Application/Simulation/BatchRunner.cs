using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
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
/// Plays a scenario's matches through the public commands and collects their results. Every random roll comes
/// from the seeds, so a batch replays identically however its matches are scheduled.
/// <para>
/// The matches run at the same time, up to <paramref name="maxParallelism"/> of them (ADR 0030), unless the
/// recorder watching them says it cannot take that. <paramref name="maxParallelism"/> defaults to the
/// machine's processor count and is a parameter so a test can ask for a degree instead of asserting on
/// whatever the host happens to have.
/// </para>
/// </summary>
public sealed class BatchRunner(
    ICommandHandler<CreateMatch, Result<MatchId>> createMatch,
    ICommandHandler<JoinMatch, Result<PlayerSlot>> joinMatch,
    IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>> boardState,
    MatchDriver driver,
    IRandomSourceFactory random,
    IAgentFactory agents,
    int? maxParallelism = null)
{
    private readonly int _maxParallelism = maxParallelism ?? Environment.ProcessorCount;

    public Task<BatchResult> RunAsync(SimulationScenario scenario, CancellationToken cancellationToken = default) =>
        RunAsync(scenario, null, cancellationToken);

    /// <summary>Plays the scenario and, when a recorder is given, records every match through it.</summary>
    public async Task<BatchResult> RunAsync(SimulationScenario scenario, IMatchRecorder? recorder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenario.Matches);
        if (scenario.Seeds is { } seeds && seeds.Count < scenario.Matches)
        {
            throw new ArgumentException($"The scenario plays {scenario.Matches} matches but lists only {seeds.Count} seeds.", nameof(scenario));
        }

        // Matches are independent: each draws from its own seed and gets its own agents, so playing several
        // at once changes how long the batch takes and nothing about what it reports. Results land in an
        // array by index rather than being appended, so the order is the seeds' order however they finish.
        // A recorder that appends every match to one artifact says no to this and gets the old one-at-a-time
        // walk, because its lines would interleave and its run would stop replaying.
        var results = new MatchResult[scenario.Matches];
        var sequential = recorder is { AllowsParallelMatches: false };
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = sequential ? 1 : _maxParallelism,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(
            Enumerable.Range(0, scenario.Matches),
            parallel,
            async (index, token) => results[index] = await PlayOneAsync(scenario, index, recorder, token));

        return new BatchResult(scenario, results, SimulationSummary.Of(results));
    }

    private async Task<MatchResult> PlayOneAsync(SimulationScenario scenario, int index, IMatchRecorder? recorder, CancellationToken cancellationToken)
    {
        var seed = scenario.SeedOf(index);
        var matchId = Accept(await createMatch.HandleAsync(new CreateMatch(scenario.RuleSet, seed), cancellationToken));
        Accept(await joinMatch.HandleAsync(new JoinMatch(matchId, PlayerId.New(), scenario.Player1Roster), cancellationToken));
        Accept(await joinMatch.HandleAsync(new JoinMatch(matchId, PlayerId.New(), scenario.Player2Roster), cancellationToken));

        var player1 = Agent(scenario.Player1Agent, scenario.RuleSet, unchecked((seed * 31) + 1));
        var player2 = Agent(scenario.Player2Agent, scenario.RuleSet, unchecked((seed * 31) + 2));
        if (recorder is not null)
        {
            player1 = recorder.Wrap(matchId, player1);
            player2 = recorder.Wrap(matchId, player2);
        }

        var outcome = Accept(await driver.PlayAsync(matchId, player1, player2, cancellationToken));

        var board = Accept(await boardState.HandleAsync(new GetBoardStateForPlayer(matchId, PlayerSlot.Player1), cancellationToken));
        if (recorder is not null)
        {
            await recorder.MatchPlayedAsync(matchId, seed, board, cancellationToken);
        }

        return new MatchResult
        {
            Index = index,
            Seed = seed,
            MatchId = matchId,
            ContentHash = board.ContentHash,
            Outcome = outcome,
            Rounds = board.RoundNumber ?? 0,
            Player1RemainingHealth = board.Allies.Sum(creature => creature.Health.Value),
            Player2RemainingHealth = board.Enemies.Sum(creature => creature.Health.Value),
        };
    }

    private IPlayerAgent Agent(AgentSpec spec, RuleSet rules, int seed) => agents.Create(spec, rules, random.Create(seed));

    private static TValue Accept<TValue>(Result<TValue> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"A simulation step was refused: {result.Error.Code} ({result.Error.Message}).");
}
