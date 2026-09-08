using DownfallArena.Application;
using DownfallArena.Application.Messaging;
using DownfallArena.Cli;
using DownfallArena.Infrastructure;
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
    await Console.Error.WriteLineAsync(CliOptions.Usage);
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

GameSession.AddTracing(builder.Services, options);

using var host = builder.Build();
var session = new GameSession(host.Services, options, seed);
session.PrintStamp();

switch (options.Command)
{
    case "play":
        await session.PlayAsync(session.RandomBot(1), session.RandomBot(2), "Random", "Random");
        return 0;
    case "human":
        await session.PlayAsync(new ConsoleAgent(Console.In, Console.Out), session.RandomBot(2), "Human", "Random");
        return 0;
    case "simulate":
        return await session.SimulateAsync();
    default:
        await Console.Error.WriteLineAsync($"Unknown command '{options.Command}'. {CliOptions.Usage}");
        return 2;
}
