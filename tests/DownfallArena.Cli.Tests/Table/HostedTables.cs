using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Resources.Authoring;
using DownfallArena.Infrastructure.Tables;
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

    /// <summary>The same rules with two picks an opportunity, the number a player can be asked one after the other.</summary>
    public static readonly RuleSet TwoPicks = RuleSet.Create(2, 2, 2, 4, 2.0);

    private readonly StudioContent _content = new();
    private readonly IHost _host;

    /// <summary>The words the content's one package is known by, published beside its schema as a build does.</summary>
    public const string PackageTags = """{ "tier:guard:v1": ["Defense", "Sustain"] }""";

    /// <summary>How the content's one taught spell is drawn, published beside its schema as a build does (ADR 0131).</summary>
    public const string SpellArt = """{ "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast" } }""";

    /// <summary>How the content's one package shows on a creature, published beside its schema as a build does (ADR 0133).</summary>
    public const string MutationArt = """{ "tier:guard:v1": { "family": "brute" } }""";

    public HostedTables(bool recording = false, RuleSet? rules = null)
        : this(recording ? directory => new FileArtifactStore(directory) : null, rules)
    {
    }

    /// <param name="store">The store a recording host writes to, made over <see cref="RunsDirectory" />.</param>
    /// <param name="rules">The rules every table is composed with, <see cref="Rules" /> unless given.</param>
    /// <param name="push">The notices the tables send (ADR 0118), or none, which is the ordinary host of these tests.</param>
    /// <param name="index">The index of the tables, a fresh one unless given: another host's, for two hosts over one store and one index.</param>
    /// <param name="turns">Where the bots' turns are taken, this process unless given: a test that takes them itself.</param>
    public HostedTables(Func<string, IArtifactStore>? store, RuleSet? rules = null, PushNotices? push = null, ReplayBasis? basis = null, InMemoryTableIndex? index = null, IAgentTurns? turns = null)
    {
        Index = index ?? new InMemoryTableIndex();
        _content.Write("presentation/package-tags.json", PackageTags);
        _content.Write("presentation/spell-art.json", SpellArt);
        _content.Write("presentation/mutation-art.json", MutationArt);
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);
        var opened = store?.Invoke(RunsDirectory);

        // A store wrapped by a test still keeps its runs under the directory it was made over, and so does the journal.
        var journal = opened is null or FileArtifactStore ? null : new FileDecisionJournal(RunsDirectory, TimeProvider.System);
        Composer = turns is null
            ? new TableComposer(_host.Services, rules ?? Rules, opened, journal) { Push = push, Basis = basis, Index = Index }
            : new TableComposer(_host.Services, rules ?? Rules, opened, journal) { Push = push, Basis = basis, Index = Index, Turns = turns };
        Stored = opened is null ? null : new StoredSessions(opened);
    }

    public TableComposer Composer { get; }

    /// <summary>The game schema the host's content was built into: what a command line names with <c>--schema</c>.</summary>
    public string SchemaPath => Path.Combine(_content.Path, "dst", "game.schema.json");

    /// <summary>
    /// The index of the tables (ADR 0161) every registry of this host lists and finds by, and the composer's runs
    /// write into: held in memory, and shared by a registry made again, as a store's index outlives its host.
    /// </summary>
    public InMemoryTableIndex Index { get; }

    /// <summary>
    /// A registry over this host's index, picking up recorded tables it does not hold when the host records: a host
    /// as it serves. Made again, it is a host started over the same store.
    /// </summary>
    public TableRegistry Registry(int mostUnderWay = TableRegistry.MostUnderWay) =>
        new(Index, mostUnderWay) { Loader = Stored is null ? null : new TableLoader(Stored, Composer, TimeProvider.System), Basis = Composer.Basis };

    /// <summary>A registry made again over the same store, after the start-up pass a host makes (<see cref="TableRestorer" />).</summary>
    public async Task<TableRegistry> RestartedAsync(int mostUnderWay = TableRegistry.MostUnderWay)
    {
        var registry = Registry(mostUnderWay);
        if (Stored is not null)
        {
            await TableRestorer.RestoreAsync(Stored, registry, TestContext.Current.CancellationToken);
        }

        return registry;
    }

    /// <summary>The store's sessions, or none for a host that records nothing.</summary>
    public StoredSessions? Stored { get; }

    /// <summary>Where a recording host writes its runs: one directory per table opened.</summary>
    public string RunsDirectory { get; } = Path.Combine(Path.GetTempPath(), $"downfall-lobby-{Guid.NewGuid():N}");

    public IServiceProvider Services => _host.Services;

    /// <summary>A table of two bots, which plays itself to an outcome in a moment.</summary>
    public static TableRequest Bots => new(AgentSpec.Parse("greedy"), AgentSpec.Parse("greedy"), Who: null, Handover: null, Seed: 7);

    /// <summary>A table with a person in seat 1, which waits at its first question until it is let go of.</summary>
    public static TableRequest OnePerson => new(null, AgentSpec.Parse("greedy"), Who: "mk", Handover: null, Seed: 7);

    /// <summary>Two people, the shape the console's "both seats here" link and the admin's hotseat link are for.</summary>
    public static TableRequest TwoPeople => new(null, null, Who: "mk", Handover: null, Seed: 7);

    public void Dispose()
    {
        _host.Dispose();
        _content.Dispose();
        DeleteRuns();
    }

    /// <summary>
    /// Removes the runs, waiting out a table still writing to them. Letting a table go does not wait for its
    /// driver (<see cref="PlayedTable.Dispose" />), so a match the test let go of can still be appending to
    /// its recording when this runs, and a file it adds under the delete fails it with "directory not empty".
    /// </summary>
    private void DeleteRuns()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(RunsDirectory))
                {
                    Directory.Delete(RunsDirectory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }
}
