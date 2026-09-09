using System.Net;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The <c>studio</c> command: serve the content studio until Ctrl+C (ADR 0015). Run it from the repository root,
/// where <c>studio/</c>, <c>viewer/</c> and the content directory are.
/// </summary>
internal static class StudioHost
{
    public const string StudioDirectory = "studio";

    public const string ViewerDirectory = "viewer";

    public const string RunsDirectory = "runs/studio";

    public static async Task<int> RunAsync(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.Data))
        {
            await Console.Error.WriteLineAsync($"Content directory '{options.Data}' does not exist. Run the studio from the repository root, or pass --data.");
            return 1;
        }

        if (!File.Exists(Path.Combine(StudioDirectory, "index.html")))
        {
            await Console.Error.WriteLineAsync($"'{StudioDirectory}/index.html' not found. Run the studio from the repository root.");
            return 1;
        }

        var store = new ContentStore(options.Data);
        var runner = new StudioRunner(options, RunsDirectory, TimeProvider.System);
        var schemaOutput = Path.GetDirectoryName(options.SchemaPath) is { Length: > 0 } directory ? directory : Path.Combine(options.Data, "dst");

        using var api = new StudioApi(store, runner, schemaOutput);
        using var server = new StudioServer(options.Port, api, new StudioFiles(StudioDirectory, ViewerDirectory), ViewerDirectory);
        using var stopping = new CancellationTokenSource();

        // Held in a local so it can be taken off the static event: a second Ctrl+C during shutdown would
        // otherwise cancel a token source that is already disposed.
        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;

        Console.WriteLine($"Content studio on {server.Url} — content '{store.Root}', schema '{options.SchemaPath}', runs under '{RunsDirectory}'.");
        Console.WriteLine("Ctrl+C to stop.");

        try
        {
            await server.RunAsync(stopping.Token);
        }
        catch (HttpListenerException exception)
        {
            await Console.Error.WriteLineAsync($"Cannot listen on {server.Url}: {exception.Message}. Another studio may be running; pass --port.");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }

        Console.WriteLine("Content studio stopped.");
        return 0;
    }
}
