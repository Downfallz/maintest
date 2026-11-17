namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The test classes that read what the host says by swapping <see cref="Console.Out" />, which is one writer for
/// the whole process: they run one at a time, after the parallel ones, so none reads another's lines or restores a
/// writer another one set. None does since #756, as the table's hosts take a sink; it stays for a class that must.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleOutput
{
    public const string Name = "Console output";
}
