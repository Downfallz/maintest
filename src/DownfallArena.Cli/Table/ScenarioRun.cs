using DownfallArena.Infrastructure.Matches.Scenarios;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One playthrough of a match scenario at the local practice table (docs/tabletop/scenarios-plan.md, S3): a
/// <see cref="ScenarioPlay" /> over a host of its own, built over the scenario's content and let go of with it.
/// </summary>
internal sealed class ScenarioRun : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly CancellationTokenSource _stopping;
    private readonly SteppedSession _session;

    private ScenarioRun(IHost host, CancellationTokenSource stopping, SteppedSession session, TableApi api, string token)
    {
        _host = host;
        _stopping = stopping;
        _session = session;
        Api = api;
        Token = token;
    }

    public TableApi Api { get; }

    public string Token { get; }

    public static async Task<ScenarioRun> StartAsync(CliOptions options, string schema, ScenarioDocument document)
    {
        var host = CliHost.Build(options with { Command = "table", Recording = false, Record = null, SchemaPath = schema }, document.Scenario.Source.Seed, logMatchToConsole: false);
        var stopping = new CancellationTokenSource();
        SteppedSession? session = null;
        try
        {
            var play = await ScenarioPlay.StartAsync(host.Services, SteppedHost.Of(host.Services), document, PracticeRun.NewToken(), label: null, stopping.Token);
            session = play.Session;
            return new ScenarioRun(host, stopping, session, play.Api, play.Token);
        }
        catch
        {
            await StopAsync(stopping, session, host);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync(_stopping, _session, _host);

    /// <summary>Lets the table go, waits for whatever step it was taking to finish, then the host.</summary>
    private static async Task StopAsync(CancellationTokenSource stopping, SteppedSession? session, IHost host)
    {
        await stopping.CancelAsync();
        try
        {
            if (session is not null)
            {
                session.Dispose();
                await Task.WhenAny(session.Outcome);
            }
        }
        finally
        {
            stopping.Dispose();
            host.Dispose();
        }
    }
}
