namespace DownfallArena.Cli.Table;

/// <summary>
/// Who takes the bots' turns once a host is started, as <see cref="TableHost.StartAsync" /> started it: what a test reads
/// rather than waiting to see whether a worker it should not have started has polled yet (#1213).
/// </summary>
internal enum TurnsTaken
{
    /// <summary>In the host's own process, as each is owed: no queue, and no worker of the turns.</summary>
    InProcess,

    /// <summary>Through the queue, by the worker this host started beside its server.</summary>
    ByThisHost,

    /// <summary>Through the queue, by <c>table-worker</c> in a process of its own: this host started no worker of the turns.</summary>
    ByTableWorker,
}
