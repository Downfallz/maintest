using DA.Game.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DA.Game.DataBuilder;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                // You can tune this level depending on how noisy you want it
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var logger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GameSchemaBuilderCli");

        // Very small "poor man's" argument parsing
        string? baseDir = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--base-dir=", StringComparison.OrdinalIgnoreCase))
            {
                baseDir = arg.Substring("--base-dir=".Length).Trim('"');
            }
        }

        try
        {
            logger.LogInformation("Running GameSchemaBuilder (baseDir: {BaseDir})", baseDir ?? "<default>");
            await GameSchemaBuilder.BuildAsync(logger, baseDir);
            logger.LogInformation("Game schema build finished.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Game schema build failed with an exception.");
            Environment.ExitCode = 1;
        }
    }
}