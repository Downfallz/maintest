using System.Globalization;
using DownfallArena.Application;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Simulation;
using DownfallArena.Cli;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
{
    await Console.Error.WriteLineAsync(exception.Message);
    await Console.Error.WriteLineAsync("Usage: play|human|simulate [--seed N] [--matches N] [--out file] [--schema path]");
    return 2;
}

if (!File.Exists(options.SchemaPath))
{
    await Console.Error.WriteLineAsync($"Game schema '{options.SchemaPath}' not found. Build it first: dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst");
    return 1;
}

var seed = options.Seed ?? Random.Shared.Next();
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services
    .AddApplication()
    .AddInfrastructure(seed)
    .AddGameResources(options.SchemaPath);
if (options.Command is "play" or "human")
{
    builder.Services.AddSingleton<IDomainEventListener>(new ConsoleMatchLog(Console.Out));
}

using var host = builder.Build();
var services = host.Services;
var resources = services.GetRequiredService<IGameResources>();
var rules = RuleSet.Default;
var roster = Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize).ToList();

switch (options.Command)
{
    case "play":
        await PlayAsync(new RandomAgent(RandomFor(1)), new RandomAgent(RandomFor(2)));
        return 0;
    case "human":
        await PlayAsync(new ConsoleAgent(Console.In, Console.Out), new RandomAgent(RandomFor(2)));
        return 0;
    case "simulate":
        await SimulateAsync();
        return 0;
    default:
        await Console.Error.WriteLineAsync($"Unknown command '{options.Command}'. Usage: play|human|simulate [--seed N] [--matches N] [--out file] [--schema path]");
        return 2;
}

async Task PlayAsync(IPlayerAgent player1, IPlayerAgent player2)
{
    Console.WriteLine($"Seed {seed}. Content {resources.Version}.");
    var matchId = Accept(await services.GetRequiredService<ICommandHandler<CreateMatch, Result<MatchId>>>().HandleAsync(new CreateMatch(rules, seed)));
    var join = services.GetRequiredService<ICommandHandler<JoinMatch, Result<PlayerSlot>>>();
    Accept(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), roster)));
    Accept(await join.HandleAsync(new JoinMatch(matchId, PlayerId.New(), roster)));

    var outcome = Accept(await services.GetRequiredService<MatchDriver>().PlayAsync(matchId, player1, player2));
    Console.WriteLine(outcome.IsDraw ? $"Draw ({outcome.Reason})." : $"{outcome.Winner} wins ({outcome.Reason}).");
}

async Task SimulateAsync()
{
    var scenario = new SimulationScenario
    {
        RuleSet = rules,
        Player1Roster = roster,
        Player2Roster = roster,
        Matches = options.Matches,
        BaseSeed = seed,
    };

    Console.WriteLine($"Simulating {scenario.Matches} match(es) from seed {seed} on content {resources.Version}...");
    var batch = await services.GetRequiredService<BatchRunner>().RunAsync(scenario);

    await using (var writer = new StreamWriter(options.Output))
    {
        BatchResultCsv.Write(batch, writer);
    }

    var summary = batch.Summary;
    Console.WriteLine($"Player1 wins {summary.Player1WinRate.ToString("P1", CultureInfo.InvariantCulture)}, Player2 wins {summary.Player2WinRate.ToString("P1", CultureInfo.InvariantCulture)}, draws {summary.DrawRate.ToString("P1", CultureInfo.InvariantCulture)}.");
    Console.WriteLine($"Rounds: average {summary.AverageRounds.ToString("F1", CultureInfo.InvariantCulture)}, min {summary.MinRounds}, max {summary.MaxRounds}.");
    Console.WriteLine($"Remaining health: Player1 {summary.AveragePlayer1RemainingHealth.ToString("F1", CultureInfo.InvariantCulture)}, Player2 {summary.AveragePlayer2RemainingHealth.ToString("F1", CultureInfo.InvariantCulture)}.");
    Console.WriteLine($"Results written to '{options.Output}'.");
}

IRandomSource RandomFor(int slot) => services.GetRequiredService<IRandomSourceFactory>().Create(unchecked((seed * 31) + slot));

static TValue Accept<TValue>(Result<TValue> result) =>
    result.IsSuccess ? result.Value : throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
