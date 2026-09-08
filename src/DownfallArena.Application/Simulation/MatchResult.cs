using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// One simulated match: how it ended, how long it took, and what health each team kept.
/// </summary>
public sealed record MatchResult(
    int Index,
    int Seed,
    MatchId MatchId,
    MatchOutcome Outcome,
    int Rounds,
    int Player1RemainingHealth,
    int Player2RemainingHealth);
