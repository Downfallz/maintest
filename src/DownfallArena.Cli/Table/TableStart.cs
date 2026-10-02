using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// How a session is started beyond its seats. The match id wanted is an earlier host's (ADR 0091), so every
/// token and page that named the match still does; null lets the match take a fresh one.
/// </summary>
internal sealed record TableStart(MatchId? MatchIdWanted = null);
