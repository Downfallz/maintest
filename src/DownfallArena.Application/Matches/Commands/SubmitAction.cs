using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Reveals the next intent of the timeline by binding its targets. The spell must be the declared one.
/// </summary>
public sealed record SubmitAction(MatchId MatchId, PlayerSlot Slot, CreatureId Actor, SpellId Spell, IReadOnlyList<CreatureId> Targets);
