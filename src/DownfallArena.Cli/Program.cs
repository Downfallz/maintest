using System.Text.Json;
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

GameSession.AddListeners(builder.Services, options);

using var host = builder.Build();

// A file an agent spec names (weights, a policy) that is missing, malformed, or trained under another feature
// schema is a user error with one line to read, not a crash with a stack.
try
{
    var session = new GameSession(host.Services, options, seed);
    session.PrintStamp();
    return await session.RunAsync();
}
catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or ArgumentException)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}
