using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Whether every creature on the timeline has declared an intent, and which ones have not.
/// </summary>
public sealed record IntentGateResult(bool CanAdvance, IReadOnlyList<CreatureId> Missing);
