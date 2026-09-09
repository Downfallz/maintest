using System.Globalization;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli;

/// <summary>
/// The commands of the console host over a built service provider: one match with a log, a batch with its CSV
/// and, when asked, a recorded run, an evaluation, or the benchmark digest check. Everything here is
/// composition; the rules live in Application.
/// </summary>
internal sealed class GameSession
{
    private readonly IServiceProvider _services;
    private readonly CliOptions _options;
    private readonly int _seed;
    private readonly IGameResources _resources;
    private readonly RuleSet _rules = RuleSet.Default;
    private readonly FeatureSchema _schema;

    public GameSession(IServiceProvider services, CliOptions options, int seed)
    {
        ArgumentNullException.ThrowIfNull(options);
        var agents = services.GetRequiredService<IAgentFactory>();
        _services = services;
        _options = options with { Player1 = agents.Resolve(options.Player1), Player2 = agents.Resolve(options.Player2) };
        _seed = seed;
        _resources = services.GetRequiredService<IGameResources>();
        _schema = FeatureSchema.Build(_resources, _rules);
    }

    private List<CreatureDefinitionId> Roster => [.. Enumerable.Repeat(_resources.Creatures.First().Id, _rules.TeamSize)];

    /// <summary>
    /// The trace recorder keeps every event of every match it sees, so it is only registered when something
    /// reads it back: a trace file or a recorded run. The combat counters listen for the commands that report
    /// fizzle and crit rates.
    /// </summary>
    public static void AddListeners(IServiceCollection serviceCollection, CliOptions cliOptions)
    {
        if (cliOptions.Trace is not null || cliOptions.Record is not null)
        {
            serviceCollection.AddSingleton<MatchTraceRecorder>();
            serviceCollection.AddSingleton<IDomainEventListener>(provider => provider.GetRequiredService<MatchTraceRecorder>());
        }

        if (cliOptions.Command is "evaluate" or "benchmark")
        {
            serviceCollection.AddSingleton<IDomainEventListener>(provider => provider.GetRequiredService<CombatStatsRecorder>());
        }
    }

    /// <summary>
    /// The stamp of this run. The session seed only matters to the commands that draw from it; an evaluation on
    /// a seed file and the benchmark print the identity of their seed set instead, with their own line.
    /// </summary>
    public void PrintStamp()
    {
        var seed = UsesSessionSeed ? $" Seed {_seed}." : string.Empty;
        Console.WriteLine($"Engine {EngineVersion.Current}. Content {_resources.Version}. Schema {_schema.Id}.{seed}");
    }

    private bool UsesSessionSeed => _options.Command is not "benchmark" && (_options.Command is not "evaluate" || _options.Seeds is null);

    /// <summary>Runs the command the options name; a command's exit code is the process's.</summary>
    public async Task<int> RunAsync()
    {
        switch (_options.Command)
        {
            case "play":
                await PlayAsync(Agent(_options.Player1, 1), Agent(_options.Player2, 2), _options.Player1.ToString(), _options.Player2.ToString());
                return 0;
            case "human":
                await PlayAsync(new ConsoleAgent(Console.In, Console.Out), Agent(_options.Player2, 2), "Human", _options.Player2.ToString());
                return 0;
            case "simulate":
                return await SimulateAsync();
            case "evaluate":
                return await EvaluateAsync();
            case "benchmark":
                return await BenchmarkAsync();
            default:
                await Console.Error.WriteLineAsync($"Unknown command '{_options.Command}'. {CliOptions.Usage}");
                return 2;
        }
    }

    public IPlayerAgent Agent(AgentSpec spec, int slot) =>
        _services.GetRequiredService<IAgentFactory>().Create(spec, _rules, _services.GetRequiredService<IRandomSourceFactory>().Create(unchecked((_seed * 31) + slot)));

    public async Task PlayAsync(IPlayerAgent player1, IPlayerAgent player2, string player1Name, string player2Name)
    {
        var matchId = Accept(await _services.GetRequiredService<ICommandHandler<CreateMatch, Result<MatchId>>>().HandleAsync(new CreateMatch(_rules, _seed)));
        var join = _services.GetRequiredService<ICommandHandler<JoinMatch, Result<PlayerSlot>>>();
        Accept(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), Roster)));
        Accept(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), Roster)));

        var outcome = Accept(await _services.GetRequiredService<MatchDriver>().PlayAsync(matchId, player1, player2));
        Console.WriteLine(outcome.IsDraw ? $"Draw ({outcome.Reason})." : $"{outcome.Winner} wins ({outcome.Reason}).");

        if (_options.Trace is { } traceFile)
        {
            await WriteTraceAsync(matchId, traceFile, Stamp(player1Name, player2Name));
        }
    }

    public async Task<int> SimulateAsync()
    {
        if (_options.Record is { } directory && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            await Console.Error.WriteLineAsync($"Run directory '{directory}' is not empty. Record each run in its own directory.");
            return 1;
        }

        var scenario = new SimulationScenario
        {
            RuleSet = _rules,
            Player1Roster = Roster,
            Player2Roster = Roster,
            Player1Agent = _options.Player1,
            Player2Agent = _options.Player2,
            Matches = _options.Matches,
            BaseSeed = _seed,
        };

        Console.WriteLine($"Simulating {scenario.Matches} match(es) from seed {_seed} on content {_resources.Version}...");
        var recorder = _options.Record is { } runDirectory ? Recorder(runDirectory, scenario) : null;
        if (recorder is not null)
        {
            await recorder.StartAsync();
        }

        var batch = await _services.GetRequiredService<BatchRunner>().RunAsync(scenario, recorder);
        if (recorder is not null)
        {
            await recorder.FinishAsync();
            Console.WriteLine($"Recorded {recorder.Steps} steps, {recorder.Episodes} episodes and {recorder.Matches} traces under '{_options.Record}'.");
        }

        await using (var writer = new StreamWriter(_options.Output))
        {
            BatchResultCsv.Write(batch, writer);
        }

        PrintSummary(batch.Summary);
        Console.WriteLine($"Results written to '{_options.Output}'.");
        return 0;
    }

    public async Task<int> EvaluateAsync()
    {
        var explicitSeeds = _options.Seeds is { } file ? BenchmarkStore.LoadSeeds(file) : null;
        IReadOnlyList<int> seeds = explicitSeeds ?? [.. Enumerable.Range(0, _options.Matches).Select(index => unchecked(_seed + index))];
        var seedSet = explicitSeeds is null ? $"seeds {_seed} to {unchecked(_seed + seeds.Count - 1)}" : $"seed set {SeedSets.IdentityOf(seeds)} from '{_options.Seeds}'";
        Console.WriteLine($"Evaluating {_options.Player1} against {_options.Player2} on {seeds.Count} seeds, mirrored ({seedSet})...");
        var evaluation = await EvaluateAsync(_options.Player1, _options.Player2, seeds, explicitSeeds is null ? _seed : SeedSets.IdentityOf(seeds));
        EvaluationConsole.Print(evaluation, Console.Out);

        var fullPath = Path.GetFullPath(_options.Output);
        await new FileArtifactWriter(Path.GetDirectoryName(fullPath) ?? ".").WriteJsonAsync(Path.GetFileName(fullPath), evaluation);
        Console.WriteLine($"Evaluation written to '{_options.Output}'.");
        return 0;
    }

    /// <summary>
    /// Plays the benchmark seeds with the baseline agents and compares the outcomes with the committed digest of
    /// this content, or writes it. A missing or different digest is a failure with the digest printed, so the
    /// intended fix is one commit away.
    /// </summary>
    public async Task<int> BenchmarkAsync()
    {
        var store = new BenchmarkStore(_options.Benchmarks);
        var seeds = store.LoadSeeds();
        Console.WriteLine($"Benchmark: {AgentSpec.Greedy} against {AgentSpec.Greedy} on {seeds.Count} seeds, mirrored (seed set {SeedSets.IdentityOf(seeds)}), content {_resources.Version}...");
        var evaluation = await EvaluateAsync(AgentSpec.Greedy, AgentSpec.Greedy, seeds, SeedSets.IdentityOf(seeds));
        EvaluationConsole.Print(evaluation, Console.Out);
        var digest = BenchmarkDigest.Of(evaluation);

        if (_options.Write)
        {
            Console.WriteLine($"Benchmark digest written to '{store.WriteDigest(digest)}'. Commit it with a journal entry.");
            return 0;
        }

        var committed = store.TryLoadDigest(_resources.Version);
        if (committed is null)
        {
            await Console.Error.WriteLineAsync($"No benchmark digest for content {_resources.Version} under '{store.Directory}'. Run 'benchmark --write' and commit '{store.DigestPath(_resources.Version)}' with a journal entry. The digest of this run follows.");
            Console.WriteLine(BenchmarkStore.Serialize(digest));
            return 1;
        }

        var differences = digest.DifferencesFrom(committed);
        if (differences.Count == 0)
        {
            Console.WriteLine($"Benchmark digest verified: {digest.Entries.Count} matches unchanged on content {_resources.Version}.");
            return 0;
        }

        await Console.Error.WriteLineAsync($"Benchmark digest differs in {differences.Count} place(s) from '{store.DigestPath(_resources.Version)}'. An intended engine or content change regenerates it with 'benchmark --write' and a journal entry.");
        foreach (var difference in differences.Take(25))
        {
            await Console.Error.WriteLineAsync("  " + difference);
        }

        return 1;
    }

    /// <summary>The stamp's base seed is the identity of the seed set when the seeds came from a file, so a repeated run stamps the same.</summary>
    private Task<EvaluationResult> EvaluateAsync(AgentSpec agentA, AgentSpec agentB, IReadOnlyList<int> seeds, int baseSeed) =>
        _services.GetRequiredService<EvaluationRunner>().RunAsync(
            new EvaluationScenario { RuleSet = _rules, Roster = Roster, AgentA = agentA, AgentB = agentB, Seeds = seeds },
            RunStamp.Create(EngineVersion.Current, _resources, _rules, _schema, agentA.ToString(), agentB.ToString(), baseSeed));

    private RunRecorder Recorder(string runDirectory, SimulationScenario scenario) =>
        new(
            new FileArtifactWriter(runDirectory),
            Stamp(scenario.Player1Agent.ToString(), scenario.Player2Agent.ToString()),
            new ObservationBuilder(_schema, _resources),
            new ActionEncoder(_schema),
            TimeProvider.System,
            _services.GetRequiredService<MatchTraceRecorder>());

    private async Task WriteTraceAsync(MatchId matchId, string traceFile, RunStamp stamp)
    {
        var trace = _services.GetRequiredService<MatchTraceRecorder>().Complete(matchId, stamp, _seed);
        var fullPath = Path.GetFullPath(traceFile);
        await new FileArtifactWriter(Path.GetDirectoryName(fullPath) ?? ".").WriteJsonAsync(Path.GetFileName(fullPath), trace);
        Console.WriteLine($"Trace written to '{traceFile}' ({trace.Entries.Count} events).");
    }

    private RunStamp Stamp(string player1Agent, string player2Agent) =>
        RunStamp.Create(EngineVersion.Current, _resources, _rules, _schema, player1Agent, player2Agent, _seed);

    private static void PrintSummary(SimulationSummary summary)
    {
        Console.WriteLine($"Player1 wins {summary.Player1WinRate.ToString("P1", CultureInfo.InvariantCulture)}, Player2 wins {summary.Player2WinRate.ToString("P1", CultureInfo.InvariantCulture)}, draws {summary.DrawRate.ToString("P1", CultureInfo.InvariantCulture)}.");
        Console.WriteLine($"Rounds: average {summary.AverageRounds.ToString("F1", CultureInfo.InvariantCulture)}, min {summary.MinRounds}, max {summary.MaxRounds}.");
        Console.WriteLine($"Remaining health: Player1 {summary.AveragePlayer1RemainingHealth.ToString("F1", CultureInfo.InvariantCulture)}, Player2 {summary.AveragePlayer2RemainingHealth.ToString("F1", CultureInfo.InvariantCulture)}.");
    }

    private static TValue Accept<TValue>(Result<TValue> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
}
