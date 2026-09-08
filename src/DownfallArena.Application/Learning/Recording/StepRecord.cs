using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// One decision as a dataset sees it: where it happened, what the player saw, what they could do, and what they did.
/// </summary>
public sealed record StepRecord
{
    public required MatchId MatchId { get; init; }

    public required PlayerSlot Slot { get; init; }

    public int? Round { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    public required ActionKind Kind { get; init; }

    public required Observation Observation { get; init; }

    /// <summary>The keys of every action the options offered, in candidate order.</summary>
    public required IReadOnlyList<string> Candidates { get; init; }

    /// <summary>The key of the chosen action, always one of <see cref="Candidates"/>.</summary>
    public required string Action { get; init; }

    public required ActionCode Code { get; init; }
}
