namespace DownfallArena.Cli.Table;

/// <summary>When a table was opened and what it was called: the two facts about a table that never change.</summary>
internal sealed record TableOpening(string Id, DateTimeOffset At);
