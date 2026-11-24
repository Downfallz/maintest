namespace DownfallArena.Cli.Table;

/// <summary>
/// What <see cref="TableHost.StartAsync" /> or <see cref="TableHost.StartWorkersAsync" /> started: the work it runs until
/// stopped, and who takes the bots' turns, said by the branch that started them (#1213).
/// </summary>
/// <param name="Running">The workers and the watch, done once they are stopped.</param>
/// <param name="Turns">Who takes the bots' turns.</param>
internal sealed record HostStarted(Task Running, TurnsTaken Turns);
