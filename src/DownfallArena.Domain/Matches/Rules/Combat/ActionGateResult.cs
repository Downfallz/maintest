using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Whether every intent of the timeline has been revealed and targeted, how many remain, and whose turn it is.
/// </summary>
public sealed record ActionGateResult(bool CanAdvance, int RemainingReveals, CreatureId? NextActor);
