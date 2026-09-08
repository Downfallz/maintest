using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

public sealed record SubmitEvolutionChoice(MatchId MatchId, PlayerSlot Slot, CreatureId Creature, SpellId Spell);
