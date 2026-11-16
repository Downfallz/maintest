using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One practice scenario being played (ADR 0079): its own host, a match set up to the question the scenario is
/// about, and a table stepped over a journal kept in memory from there (ADR 0159). Nothing is recorded.
/// </summary>
internal sealed class PracticeRun : IAsyncDisposable
{
    private const string Run = "practice";

    private readonly IHost _host;
    private readonly CancellationTokenSource _stopping;
    private readonly SteppedSession _session;

    private PracticeRun(IHost host, CancellationTokenSource stopping, SteppedSession session, TableApi api, string token, PracticeScenario scenario)
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
        SteppedSession? session = null;
        try
        {
            var services = host.Services;
            var resources = services.GetRequiredService<IGameResources>();
            var rules = scenario.Rules;
            var stepped = SteppedHost.Of(services);
            var journal = new InMemoryDecisionJournal();
            List<CreatureDefinitionId> roster = [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)];
            var match = new JournaledMatch(Run, MatchId.New(), rules, PracticeScenario.Seed, roster);
            var fallback = new GreedyAgent(resources, rules);
            await PreparedAsync(stepped, journal, match, scenario, fallback, stopping.Token);

            var person = new SteppedPerson();
            person.Arrive();
            var token = NewToken();
            TableSeat[] seats = [new TableSeat(PlayerSlot.Player1, token, person), new TableSeat(PlayerSlot.Player2, NewToken(), null)];
            var plan = new SeatPlan(SeatPlan.Person(Run), new Occupant(new PracticeAgent(scenario, fallback), Run));
            var opened = await SteppedSession.OpenAsync(stepped, journal, match, new SteppedSeats(plan, seats, static (_, _) => null), stopping: stopping.Token);
            session = opened.IsSuccess ? opened.Value : throw new InvalidDataException($"Practice '{scenario.Id}' could not be opened: {opened.Error.Message}");
            session.Begin();
            if (person.Waiting is null)
            {
                throw new InvalidDataException($"Practice '{scenario.Id}' ended before reaching its question.");
            }

            var api = new TableApi(session, seats, CatalogueProjection.Build(resources, rules), services.GetRequiredService<MatchTraceRecorder>())
            {
                Guide = new DecisionGuideProjection(resources, rules),
                FeedStart = session.Journaled ?? 0,
            };
            return new PracticeRun(host, stopping, session, api, token, scenario);
        }
        catch
        {
            await StopAsync(stopping, session, host);
            throw;
        }
    }

    /// <summary>
    /// Plays the scenario's preparation into the journal: both seats answered by the recipe, through the gates
    /// any decision goes through, until the person's seat is asked the question the scenario hands them. Then the
    /// match is let go of, so the table opens it from its journal like any other.
    /// </summary>
    /// <remarks>
    /// The two seats are answered one question at a time, the person's seat first. The other seat may still owe answers to what both are asked at once when the person's question comes;
    /// the table's own agent gives them once it begins.
    /// </remarks>
    private static async Task PreparedAsync(SteppedHost stepped, InMemoryDecisionJournal journal, JournaledMatch match, PracticeScenario scenario, IPlayerAgent fallback, CancellationToken cancellationToken)
    {
        var preparing = stepped.Stepper(journal);
        var recipe = new PracticeAgent(scenario, fallback);
        try
        {
            var state = Read(await preparing.ReadAsync(match, cancellationToken), scenario);
            while (true)
            {
                if (state.Outcome is not null)
                {
                    throw new InvalidDataException($"Practice '{scenario.Id}' ended before reaching its question.");
                }

                var person = state.Player1.View;
                if (person.Asked is { } asked && scenario.HandsOver(person.Board, asked))
                {
                    return;
                }

                var slot = person.Asked is not null ? PlayerSlot.Player1 : PlayerSlot.Player2;
                var view = state.Of(slot).View;
                if (view.Asked is null)
                {
                    throw new InvalidDataException($"Practice '{scenario.Id}' stopped where nobody is asked anything.");
                }

                var step = await preparing.SubmitAsync(match, MatchSeats.People, new SeatDecision(slot, SeatAnswer.Of(recipe, view)), cancellationToken: cancellationToken);
                state = step.IsSuccess
                    ? step.Value.State
                    : throw new InvalidDataException($"Practice '{scenario.Id}' was refused a step of its recipe: {step.Error.Message}");
            }
        }
        finally
        {
            await preparing.ForgetAsync(match.MatchId, CancellationToken.None);
        }
    }

    private static JournaledState Read(SharedKernel.Primitives.Result<JournaledState> read, PracticeScenario scenario) =>
        read.IsSuccess ? read.Value : throw new InvalidDataException($"Practice '{scenario.Id}' could not be set up: {read.Error.Message}");

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
