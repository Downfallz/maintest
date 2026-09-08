using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// What one player sees of a match: both teams as snapshots, the round position, their own hidden choices, and
/// everything already public (timeline, revealed actions, outcome).
/// </summary>
public sealed record PlayerBoardState
{
    public required MatchId MatchId { get; init; }

    public required PlayerSlot Slot { get; init; }

    public required MatchState State { get; init; }

    public required string ContentHash { get; init; }

    public int? RoundNumber { get; init; }

    public RoundPhase? Phase { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    public required IReadOnlyList<CreatureSnapshot> Allies { get; init; }

    public required IReadOnlyList<CreatureSnapshot> Enemies { get; init; }

    /// <summary>The player's own evolution choices this round.</summary>
    public IReadOnlyList<EvolutionChoice> EvolutionChoices { get; init; } = [];

    public bool HasPassedEvolution { get; init; }

    /// <summary>The player's own speed choices this round.</summary>
    public IReadOnlyList<SpeedChoice> SpeedChoices { get; init; } = [];

    /// <summary>The player's own intents, hidden from the other player until revealed.</summary>
    public IReadOnlyList<CombatIntent> Intents { get; init; } = [];

    public IReadOnlyList<ActivationSlot> Timeline { get; init; } = [];

    /// <summary>The actions revealed so far this round, in timeline order; public to both players.</summary>
    public IReadOnlyList<CombatAction> RevealedActions { get; init; } = [];

    public int RevealCursor { get; init; }

    public int ResolveCursor { get; init; }

    public MatchOutcome? Outcome { get; init; }
}
