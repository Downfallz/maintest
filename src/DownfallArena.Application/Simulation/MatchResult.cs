using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// One simulated match: which content and seed produced it, how it ended, how long it took, and what health
/// each team kept. The content hash and the seed are what a replay needs.
/// </summary>
public sealed record MatchResult
{
    public required int Index { get; init; }

    public required int Seed { get; init; }

    public required MatchId MatchId { get; init; }

    public required string ContentHash { get; init; }

    public required MatchOutcome Outcome { get; init; }

    public required int Rounds { get; init; }

    public required int Player1RemainingHealth { get; init; }

    public required int Player2RemainingHealth { get; init; }
}
