namespace DownfallArena.Cli.Table;

/// <summary>Where a table's bots' turns are taken (ADR 0159, phase 5; ADR 0162). <c>--turns process|queue|worker</c>.</summary>
internal enum TurnsMode
{
    /// <summary>In the host's own process, as each is owed.</summary>
    Process,

    /// <summary>Through a queue, taken by a worker in the host's own process: one process does it all.</summary>
    Queue,

    /// <summary>Through a queue, taken by <c>table-worker</c> in a process of its own: the host runs no worker.</summary>
    Worker,
}
