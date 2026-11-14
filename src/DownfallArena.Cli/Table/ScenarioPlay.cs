using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One playthrough of a match scenario opened over services already built for its content (docs/tabletop/
/// scenarios-plan.md, S3, S4): a match started from the scenario under an id of its own and held to its boards, a
/// journal kept in memory that begins with the match's entries before the cut, the person in the scenario's seat and
/// the scripted opponent in the other. Nothing is recorded, and nothing reaches the session it was cut from.
/// </summary>
internal sealed record ScenarioPlay(SteppedSession Session, TableApi Api, string Token, PlayerSlot Seat)
{
    private const string Run = "scenario";

    /// <param name="label">What the seat's view says the table is, so the page shows it as practice; none at the local table.</param>
    public static async Task<ScenarioPlay> StartAsync(IServiceProvider services, SteppedHost stepped, ScenarioDocument document, string token, PracticeLabel? label, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(stepped);
        ArgumentNullException.ThrowIfNull(document);
        var scenario = document.Scenario;
        var resources = services.GetRequiredService<IGameResources>();
        var rules = scenario.Source.Rules;
        var started = await services.GetRequiredService<ScenarioStarter>().StartAsync(scenario, Run, MatchId.New(), stopping);
        if (started.IsFailure)
        {
            throw new InvalidDataException($"The scenario cannot be played on this engine: {started.Error.Message}");
        }

        var journal = new InMemoryDecisionJournal();
        await journal.AppendAsync(Run, JournalPosition.Start, started.Value.Entries, stopping);

        var opponent = ScriptedOpponent.Of(scenario, Other(document.Seat), new GreedyAgent(resources, rules));
        opponent.HandedOver += (_, _) => Console.WriteLine("  The opponent's recorded answers no longer fit the match: greedy plays it from here.");
        var person = new SteppedPerson();
        person.Arrive();
        var personSeat = new TableSeat(document.Seat, token, person);
        var botSeat = new TableSeat(Other(document.Seat), PracticeRun.NewToken(), null);
        TableSeat[] seats = document.Seat == PlayerSlot.Player1 ? [personSeat, botSeat] : [botSeat, personSeat];
        var bot = new Occupant(opponent, Run);
        var plan = document.Seat == PlayerSlot.Player1 ? new SeatPlan(SeatPlan.Person(Run), bot) : new SeatPlan(bot, SeatPlan.Person(Run));
        var opened = await SteppedSession.OpenAsync(stepped, journal, started.Value.Match, new SteppedSeats(plan, seats, static (_, _) => null), stopping: stopping);
        var session = opened.IsSuccess ? opened.Value : throw new InvalidDataException($"The scenario could not be opened: {opened.Error.Message}");
        try
        {
            session.Begin();
            var api = new TableApi(session, seats, CatalogueProjection.Build(resources, rules), stepped.Events)
            {
                Guide = new DecisionGuideProjection(resources, rules),
                FeedStart = session.Journaled ?? 0,
                Practice = label,
            };
            return new ScenarioPlay(session, api, token, document.Seat);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Lets the playthrough go, and waits for whatever step it was taking to finish.</summary>
    public async Task StopAsync()
    {
        Session.Dispose();
        await Task.WhenAny(Session.Outcome);
    }

    private static PlayerSlot Other(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;
}
