using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Table;

/// <summary>A restart owns a fresh engine, including its random stream and event recorder.</summary>
internal sealed class PracticeRun : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly CancellationTokenSource _stopping;
    private readonly TableSession _session;

    private PracticeRun(IHost host, CancellationTokenSource stopping, TableSession session, TableApi api, string token, PracticeScenario scenario)
    {
        _host = host;
        _stopping = stopping;
        _session = session;
        Api = api;
        Token = token;
        Scenario = scenario;
    }

    public TableApi Api { get; }

    public string Token { get; }

    public PracticeScenario Scenario { get; }

    public static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static async Task<PracticeRun> StartAsync(CliOptions options, PracticeScenario scenario)
    {
        var host = CliHost.Build(options with { Command = "table", Recording = false, Record = null }, PracticeScenario.Seed, logMatchToConsole: false);
        var stopping = new CancellationTokenSource();
        TableSession? session = null;
        try
        {
            var services = host.Services;
            var resources = services.GetRequiredService<IGameResources>();
            var rules = PracticeScenario.Rules;
            var person = new HumanSeat(stopping.Token);
            var fallback = new GreedyAgent(resources, rules);
            var player1 = new SeatAgent(new Occupant(new PracticeAgent(scenario, fallback, person), "practice"));
            var player2 = new SeatAgent(new Occupant(new PracticeAgent(scenario, fallback), "practice"));
            session = await TableSession.StartAsync(services, rules, PracticeScenario.Seed, player1, player2, cancellationToken: stopping.Token);
            var token = NewToken();
            var seats = new[] { new TableSeat(PlayerSlot.Player1, token, person), new TableSeat(PlayerSlot.Player2, NewToken(), null) };

            // A recipe that stopped short must fail here, not leave a browser waiting for a question forever.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (person.Waiting is null)
            {
                if (session.Outcome.IsCompleted)
                {
                    await session.Outcome;
                    throw new InvalidDataException($"Practice '{scenario.Id}' ended before reaching its question.");
                }

                await Task.Delay(10, timeout.Token);
            }

            var events = services.GetRequiredService<MatchTraceRecorder>();
            var api = new TableApi(session, session.Queries, seats, CatalogueProjection.Build(resources, rules), events,
                guide: new DecisionGuideProjection(resources, rules), feedStart: events.EntriesOf(session.MatchId).Count);
            return new PracticeRun(host, stopping, session, api, token, scenario);
        }
        catch
        {
            await StopAsync(stopping, session, host);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync(_stopping, _session, _host);

    private static async Task StopAsync(CancellationTokenSource stopping, TableSession? session, IHost host)
    {
        await stopping.CancelAsync();
        try
        {
            if (session is not null)
            {
                await session.Outcome;
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // A person waiting for input releases the driver on cancellation.
        }
        finally
        {
            session?.Dispose();
            stopping.Dispose();
            host.Dispose();
        }
    }
}
