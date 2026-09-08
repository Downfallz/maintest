using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

public sealed record SubmitIntent(MatchId MatchId, PlayerSlot Slot, CreatureId Actor, SpellId Spell);
