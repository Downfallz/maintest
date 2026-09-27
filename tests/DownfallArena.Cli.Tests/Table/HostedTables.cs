using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Resources.Authoring;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A host with content built and a composer over it, for the tests of what sits above one table: the registry
/// and the lobby. Tables are composed for real, through the engine, with bots so a match ends on its own and
/// people where a seat has to stay open.
/// </summary>
internal sealed class HostedTables : IDisposable
{
    public static readonly RuleSet Rules = RuleSet.Create(2, 2, 1, 4, 2.0);

    private readonly StudioContent _content = new();
    private readonly IHost _host;

    public HostedTables(bool recording = false)
        : this(recording ? directory => new FileArtifactStore(directory) : null)
    {
    }

    /// <param name="store">The store a recording host writes to, made over <see cref="RunsDirectory" />.</param>
    public HostedTables(Func<string, IArtifactStore>? store)
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);
        Composer = new TableComposer(_host.Services, Rules, store?.Invoke(RunsDirectory));
    }

    public TableComposer Composer { get; }

    /// <summary>Where a recording host writes its runs: one directory per table opened.</summary>
    public string RunsDirectory { get; } = Path.Combine(Path.GetTempPath(), $"downfall-lobby-{Guid.NewGuid():N}");

    public IServiceProvider Services => _host.Services;

    /// <summary>A table of two bots, which plays itself to an outcome in a moment.</summary>
    public static TableRequest Bots => new(AgentSpec.Parse("greedy"), AgentSpec.Parse("greedy"), Who: null, Handover: null, Seed: 7);

    /// <summary>A table with a person in seat 1, which waits at its first question until it is let go of.</summary>
    public static TableRequest OnePerson => new(null, AgentSpec.Parse("greedy"), Who: "mk", Handover: null, Seed: 7);

    public void Dispose()
    {
        _host.Dispose();
        _content.Dispose();
        if (Directory.Exists(RunsDirectory))
        {
            Directory.Delete(RunsDirectory, recursive: true);
        }
    }
}
