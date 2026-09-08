using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The round moved to a sub-phase. Raised for every transition, so the UI and bots follow one event.
/// </summary>
public sealed record SubPhaseEntered(MatchId MatchId, RoundId Round, RoundSubPhase SubPhase) : IDomainEvent;
