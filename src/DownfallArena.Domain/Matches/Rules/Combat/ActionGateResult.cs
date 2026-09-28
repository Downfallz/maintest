using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Whether every slot of the timeline has been activated, how many remain, and whose turn it is.
/// </summary>
public sealed record ActionGateResult(bool CanAdvance, int RemainingSlots, CreatureId? NextActor);
