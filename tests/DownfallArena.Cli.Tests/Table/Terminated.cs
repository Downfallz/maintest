namespace DownfallArena.Cli.Tests.Table;

/// <summary>The tests that run a process taking from the emulator's shared queues: run alone, after the others (#1205).</summary>
[CollectionDefinition(Alone, DisableParallelization = true)]
public sealed class Terminated
{
    public const string Alone = "Terminated alone";
}
