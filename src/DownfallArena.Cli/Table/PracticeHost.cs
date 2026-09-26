using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Table;

internal static class PracticeHost
{
    public static async Task<int> RunAsync(CliOptions options)
    {
        using var stopping = new CancellationTokenSource();
        var token = PracticeRun.NewToken();
        await using var table = new PracticeTable(options, token);
        var files = new TableFiles(TableHost.TableDirectory);
        using var server = new HttpHost(options.Bind, options.Port, async (request, body) =>
        {
            var path = request.Url?.AbsolutePath ?? "/";
            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                return files.Get(path);
            }
            var refusal = HttpHost.CrossSite(request.Headers["Sec-Fetch-Site"], request.HttpMethod, request.ContentType, "table");
            return refusal ?? await table.HandleAsync(request.HttpMethod, path, body, request.Headers[TableApi.TokenHeader], request.Headers["If-None-Match"], request.Url?.Query);
        });

        ConsoleCancelEventHandler cancel = (_, args) => { args.Cancel = true; stopping.Cancel(); };
        Console.CancelKeyPress += cancel;
        Console.WriteLine($"Practice table: {server.Url}?practice={token}");
        Console.WriteLine($"Four reproducible scenarios. Seed {PracticeScenario.Seed}. Nothing is recorded. Ctrl+C to stop.");
        try
        {
            await server.RunAsync(stopping.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }

        return 0;
    }
}
