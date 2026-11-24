namespace DownfallArena.Cli.Table;

/// <summary>What <see cref="TableHost.StartAsync" /> started beside a host's server: the work it runs until stopped, and who takes the bots' turns.</summary>
/// <param name="Running">The workers and the watch, done once they are stopped.</param>
/// <param name="Turns">Who takes the bots' turns, as the host started them (#1213).</param>
internal sealed record HostStarted(Task Running, TurnsTaken Turns);
