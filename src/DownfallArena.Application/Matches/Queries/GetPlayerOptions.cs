using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Queries;

public sealed record GetPlayerOptions(MatchId MatchId, PlayerSlot Slot);
