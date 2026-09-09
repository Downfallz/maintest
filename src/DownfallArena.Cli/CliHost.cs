using DownfallArena.Application;
using DownfallArena.Application.Messaging;
using DownfallArena.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DownfallArena.Cli;

/// <summary>
/// The composition of a single run: use cases, adapters, the game resources of one schema file, and the
/// listeners the command asks for. The console commands build one; the studio builds one per run, because a
/// rebuilt schema is a different catalogue (ADR 0015).
/// </summary>
internal static class CliHost
{
    public static IHost Build(CliOptions options, int seed, bool logMatchToConsole)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddApplication()
            .AddInfrastructure(seed)
            .AddGameResources(options.SchemaPath);

        if (logMatchToConsole)
        {
            builder.Services.AddSingleton<IDomainEventListener>(new ConsoleMatchLog(Console.Out));
        }

        GameSession.AddListeners(builder.Services, options);
        return builder.Build();
    }
}
