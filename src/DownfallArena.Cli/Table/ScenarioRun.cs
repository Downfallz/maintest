using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One playthrough of a match scenario (docs/tabletop/scenarios-plan.md, S3): its own host over the scenario's content,
/// a match started from the scenario under an id of its own and held to its boards, a journal kept in memory that begins
/// with the entries before the cut, the person in the scenario's seat and the scripted opponent in the other. Nothing is
/// recorded, and nothing reaches the session the scenario was cut from.
/// </summary>
internal sealed class ScenarioRun : IAsyncDisposable
{
    private const string Run = "scenario";

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
        var scenario = document.Scenario;
        var host = CliHost.Build(options with { Command = "table", Recording = false, Record = null, SchemaPath = schema }, scenario.Source.Seed, logMatchToConsole: false);
        var stopping = new CancellationTokenSource();
        SteppedSession? session = null;
        try
        {
            var services = host.Services;
            var resources = services.GetRequiredService<IGameResources>();
            var rules = scenario.Source.Rules;
            var started = await services.GetRequiredService<ScenarioStarter>().StartAsync(scenario, Run, MatchId.New(), stopping.Token);
            if (started.IsFailure)
            {
                throw new InvalidDataException($"The scenario cannot be played on this engine: {started.Error.Message}");
            }

            var journal = new InMemoryDecisionJournal();
            await journal.AppendAsync(Run, JournalPosition.Start, started.Value.Entries, stopping.Token);

            var opponent = ScriptedOpponent.Of(scenario, Other(document.Seat), new GreedyAgent(resources, rules));
            opponent.HandedOver += (_, _) => Console.WriteLine("  The opponent's recorded answers no longer fit the match: greedy plays it from here.");
            var person = new SteppedPerson();
            person.Arrive();
            var token = PracticeRun.NewToken();
            var personSeat = new TableSeat(document.Seat, token, person);
            var botSeat = new TableSeat(Other(document.Seat), PracticeRun.NewToken(), null);
            TableSeat[] seats = document.Seat == PlayerSlot.Player1 ? [personSeat, botSeat] : [botSeat, personSeat];
            var bot = new Occupant(opponent, Run);
            var plan = document.Seat == PlayerSlot.Player1 ? new SeatPlan(SeatPlan.Person(Run), bot) : new SeatPlan(bot, SeatPlan.Person(Run));
            var stepped = SteppedHost.Of(services);
            var opened = await SteppedSession.OpenAsync(stepped, journal, started.Value.Match, new SteppedSeats(plan, seats, static (_, _) => null), stopping: stopping.Token);
            session = opened.IsSuccess ? opened.Value : throw new InvalidDataException($"The scenario could not be opened: {opened.Error.Message}");
            session.Begin();

            var api = new TableApi(session, seats, CatalogueProjection.Build(resources, rules), services.GetRequiredService<MatchTraceRecorder>())
            {
                Guide = new DecisionGuideProjection(resources, rules),
                FeedStart = session.Journaled ?? 0,
            };
            return new ScenarioRun(host, stopping, session, api, token);
        }
        catch
        {
            await StopAsync(stopping, session, host);
            throw;
        }
    }

    private static PlayerSlot Other(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

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
