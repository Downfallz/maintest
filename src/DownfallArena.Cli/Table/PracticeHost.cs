using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Table;

internal static class PracticeHost
{
    public static async Task<int> RunAsync(CliOptions options)
    {
        var token = PracticeRun.NewToken();
        await using var table = new PracticeTable(options, token);
        return await ServeAsync(options, table, token, $"{PracticeScenario.All.Count} reproducible scenarios. Seed {PracticeScenario.Seed}. Nothing is recorded. Ctrl+C to stop.");
    }

    /// <summary>
    /// <c>table --scenario</c> (docs/tabletop/scenarios-plan.md, S3): the practice page on one scenario file, started
    /// at once on the content it names, and nothing recorded.
    /// </summary>
    public static async Task<int> RunScenarioAsync(CliOptions options)
    {
        Infrastructure.Matches.Scenarios.ScenarioDocument document;
        string schema;
        try
        {
            document = Infrastructure.Matches.Scenarios.ScenarioFile.Read(await File.ReadAllTextAsync(options.Scenario!));
            schema = await ScenarioContent.SchemaOfAsync(document, options);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        {
            await Console.Error.WriteLineAsync(failure.Message);
            return 1;
        }

        var token = PracticeRun.NewToken();
        await using var table = new ScenarioTable(options, schema, document, token);
        try
        {
            await table.StartAsync();
        }
        catch (InvalidDataException failure)
        {
            await Console.Error.WriteLineAsync(failure.Message);
            return 1;
        }

        var cut = document.Scenario.Cut;
        return await ServeAsync(options, table, token,
            $"Scenario: {document.Title ?? $"session {document.Session ?? document.Scenario.Source.Run}"}, round {cut.Round}, played as {ScenarioCommands.SeatName(document.Seat)}. Seed {document.Scenario.Source.Seed}. Nothing is recorded. Ctrl+C to stop.");
    }

    private static async Task<int> ServeAsync(CliOptions options, IPracticeTable table, string token, string about)
    {
        using var stopping = new CancellationTokenSource();
        var files = new TableFiles(TableHost.TableDirectory);
        using var server = new HttpHost(options.Bind, options.Port, async (request, body) =>
        {
            var path = request.Url?.AbsolutePath ?? "/";
            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                return files.Get(path, request.Headers["If-None-Match"], request.Headers["Accept-Encoding"]);
            }
            var refusal = HttpHost.CrossSite(request.Headers["Sec-Fetch-Site"], request.HttpMethod, request.ContentType, "table");
            return refusal ?? await table.HandleAsync(request.HttpMethod, path, body, request.Headers[TableApi.TokenHeader], request.Headers["If-None-Match"], request.Url?.Query);
        });

        ConsoleCancelEventHandler cancel = (_, args) => { args.Cancel = true; stopping.Cancel(); };
        Console.CancelKeyPress += cancel;
        Console.WriteLine($"Practice table: {server.Url}?practice={token}");
        Console.WriteLine(about);
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
