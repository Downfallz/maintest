using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

public sealed record PassEvolution(MatchId MatchId, PlayerSlot Slot);
