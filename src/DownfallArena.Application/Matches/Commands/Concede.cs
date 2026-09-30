using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>A player gives the match up; the other player wins on the spot (ADR 0087).</summary>
public sealed record Concede(MatchId MatchId, PlayerSlot Slot);
