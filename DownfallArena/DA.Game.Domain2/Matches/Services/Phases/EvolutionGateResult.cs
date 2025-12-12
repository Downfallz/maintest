namespace DA.Game.Domain2.Matches.Services.Phases;

/// <summary>
/// Result of the evolution (spell unlock) gate evaluation for both players.
/// </summary>
public sealed record EvolutionGateResult(
    bool CanAdvance,
    int Player1RemainingPicks,
    int Player2RemainingPicks
);