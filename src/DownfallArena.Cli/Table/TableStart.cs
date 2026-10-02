using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// How a session is started beyond its seats. The match id wanted is an earlier host's (ADR 0091), so every
/// token and page that named the match still does; null lets the match take a fresh one. A session that waits
/// to begin asks nobody anything until it is told to (ADR 0092): the table's people have to reach their seats.
/// </summary>
internal sealed record TableStart(MatchId? MatchIdWanted = null, bool WaitToBegin = false);
