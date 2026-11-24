namespace DownfallArena.Cli.Table;

/// <summary>
/// Who takes the bots' turns once a host is started, as <see cref="TableHost.StartAsync" /> started them. A test reads
/// it, so it does not have to wait and see whether a worker that should not exist has polled (#1213).
/// </summary>
internal enum TurnsTaken
{
    /// <summary>In the host's own process, as each falls due: no queue, and no worker of the turns.</summary>
    InProcess,

    /// <summary>Through the queue, by the worker this host started beside its server.</summary>
    ByThisHost,

    /// <summary>Through the queue, by <c>table-worker</c> in a process of its own: this host started no worker of the turns.</summary>
    ByTableWorker,
}
