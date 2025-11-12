using DA.Game.Application.Matches.Simulation.Results;
using DA.Game.Application.Matches.Simulation.Scenarios;
using DA.Game.Domain2.Match.Enums;

namespace DA.Game.Application.Matches.Simulation.Runners;
// Application/Matches/Simulation/Runners/BatchRunner.cs
public sealed class BatchRunner(ISimulationRunner runner)
{
    public async Task<BatchResult> RunAsync(MatchScenario scenario, int runs, CancellationToken ct = default)
    {
        var results = new List<MatchResult>(runs);
        for (int i = 0; i < runs; i++)
            results.Add(await runner.RunAsync(scenario, ct));

        var started = results.Count(r => r.FinalState != MatchState.WaitingForPlayers);
        var finished = results.Count(r => r.FinalState == MatchState.Ended); // plus tard quand tu auras un état "Finished"
        var avgTurns = results.Count > 0 ? results.Average(r => r.TurnsPlayed) : 0;

        return new BatchResult(
            Scenario: scenario.Name,
            Runs: runs,
            AvgTurns: avgTurns,
            Started: started,
            Finished: finished
        );
    }
}
