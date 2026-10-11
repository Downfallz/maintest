using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The slot that came up was passed over with no action: its creature declared no intent, under a rule set that
/// declares none, and had nothing it could cast -- dead, stunned, or with no spell it could pay for on a legal
/// target (ADR 0110). Public, like a reveal. Nothing resolves, so no <see cref="CombatActionResolved" /> follows.
/// </summary>
public sealed record SlotSkipped(MatchId MatchId, RoundId RoundId, CreatureId Actor, DomainError Reason) : IMatchEvent;
