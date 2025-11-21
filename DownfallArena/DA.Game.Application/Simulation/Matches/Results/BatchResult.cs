namespace DA.Game.Application.Simulation.Matches.Results;

public sealed record BatchResult(
    string Scenario,
    int Runs,
    double AvgTurns,
    int Started,
    int Finished
);