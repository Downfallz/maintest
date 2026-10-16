using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// One slot as a searching agent read it under a rule set that chooses spells at the slot (ADR 0110): the
/// spell it chose and, for every spell it could cast there, what that spell's best target set was worth.
/// </summary>
public sealed record SlotChoice(MatchId Match, int Round, PlayerSlot Seat, CreatureId Actor, SpellId Chosen, IReadOnlyList<SpellValue> Spells);
