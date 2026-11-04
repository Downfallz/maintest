using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>Ordinary round energy applied before ongoing effects, including zero gains.</summary>
public sealed record RoundEnergyGranted(MatchId MatchId, RoundId RoundId, IReadOnlyList<RoundEnergyGrant> Grants) : IMatchEvent;
