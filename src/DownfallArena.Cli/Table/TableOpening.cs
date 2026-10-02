namespace DownfallArena.Cli.Table;

/// <summary>
/// When a table was opened, what it was called, and what was asked for: the facts about a table that never
/// change. The request is null for a table nobody asked this host for by request, the practice table.
/// </summary>
internal sealed record TableOpening(string Id, DateTimeOffset At, TableRequest? Request = null);
