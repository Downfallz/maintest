using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>The players who let their time to answer run out lose the match, or draw it when both did (ADR 0169).</summary>
public sealed record TimeOut(MatchId MatchId, IReadOnlyList<PlayerSlot> Late);
