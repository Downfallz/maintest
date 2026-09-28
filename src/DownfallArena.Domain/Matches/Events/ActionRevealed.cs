using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The intent of the slot that came up was revealed and bound to its targets -- none when its creature could not
/// act -- and resolves right after it (ADR 0083).
/// </summary>
public sealed record ActionRevealed(MatchId MatchId, RoundId RoundId, CombatAction Action) : IMatchEvent;
