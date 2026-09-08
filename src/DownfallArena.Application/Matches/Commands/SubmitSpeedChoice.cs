using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

public sealed record SubmitSpeedChoice(MatchId MatchId, PlayerSlot Slot, CreatureId Creature, Speed Speed);
