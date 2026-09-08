using System.Globalization;
using DownfallArena.Application.Agents;
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
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli;

/// <summary>
/// The commands of the console host over a built service provider: one match with a log, or a batch with its
/// CSV and, when asked, a recorded run. Everything here is composition; the rules live in Application.
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
        _services = services;
        _options = options;
        _seed = seed;
        _resources = services.GetRequiredService<IGameResources>();
        _schema = FeatureSchema.Build(_resources, _rules);
    }

    private List<CreatureDefinitionId> Roster => [.. Enumerable.Repeat(_resources.Creatures.First().Id, _rules.TeamSize)];

    /// <summary>
    /// The trace recorder keeps every event of every match it sees, so it is only registered when something
    /// reads it back: a trace file or a recorded run.
    /// </summary>
    public static void AddTracing(IServiceCollection serviceCollection, CliOptions cliOptions)
    {
        if (cliOptions.Trace is not null || cliOptions.Record is not null)
        {
            serviceCollection.AddSingleton<MatchTraceRecorder>();
            serviceCollection.AddSingleton<IDomainEventListener>(provider => provider.GetRequiredService<MatchTraceRecorder>());
        }
    }

    public void PrintStamp() =>
        Console.WriteLine($"Engine {EngineVersion.Current}. Content {_resources.Version}. Schema {_schema.Id}. Seed {_seed}.");

    public IPlayerAgent RandomBot(int slot) =>
        new RandomAgent(_services.GetRequiredService<IRandomSourceFactory>().Create(unchecked((_seed * 31) + slot)));

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
