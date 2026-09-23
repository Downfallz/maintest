using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Whether the TieOrder sub-phase is complete, and which players still have a tie order to give (ADR 0063).
/// </summary>
public sealed record TieOrderGateResult(bool CanAdvance, IReadOnlyList<PlayerSlot> Waiting);
