using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Driving;

/// <summary>
/// The stepper plays the match the driver plays at a table, without anything waiting between two decisions
/// (ADR 0159): held to the driver event for event, with Player agents and with people in the seats.
/// </summary>
public sealed partial class MatchStepperTests
{
    public static TheoryData<uint, CombatDeclaration> Matches
    {
        get
        {
            var matches = new TheoryData<uint, CombatDeclaration>();
            foreach (var seed in new uint[] { 1, 2, 3, 5, 8, 13 })
            {
                matches.Add(seed, CombatDeclaration.HiddenIntent);
                matches.Add(seed, CombatDeclaration.AtActivation);
            }

            return matches;
        }
    }

    [Theory]
    [MemberData(nameof(Matches))]
    public async Task Two_Player_agents_stepped_play_the_match_the_driver_plays_at_a_table(uint seed, CombatDeclaration declaration)
    {
        var driven = await DrivenAsync(seed, declaration);

        var stepped = await SteppedAsync(seed, declaration, person1: false, person2: false);

        stepped.Outcome.ShouldBe(driven.Outcome);
        stepped.Events.ShouldBe(driven.Events);
    }

    /// <summary>
    /// A person in a seat is asked what the driver would ask the agent there, on the board it would be shown: an
    /// agent answering each question through the seat's view plays the match it plays when seated itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_seat_answered_from_outside_is_asked_what_its_Player_agent_would_be(uint seed, CombatDeclaration declaration)
    {
        var driven = await DrivenAsync(seed, declaration);

        foreach (var (person1, person2) in new[] { (true, false), (false, true), (true, true) })
        {
            var stepped = await SteppedAsync(seed, declaration, person1, person2);

            stepped.Outcome.ShouldBe(driven.Outcome);
            stepped.Events.ShouldBe(driven.Events);
        }
    }

    /// <summary>
    /// What a table writes down is what rebuilds it: every decision of a match, the agents' included, submitted
    /// again in order to two seats nobody answers for gives the same match, with no agent asked anything.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task The_decisions_of_a_match_submitted_again_rebuild_it(uint seed, CombatDeclaration declaration)
    {
        var played = await SteppedAsync(seed, declaration, person1: true, person2: false);
        var store = new MatchStore();
        var match = store.Started(Rules(declaration), new TestRandom(seed));
        var log = Logged(store);
        var stepper = Stepper(store);
        var held = HeldAnswers.None;
        MatchStep? last = null;

        foreach (var decision in played.Decided)
        {
            var step = await stepper.SubmitAsync(match.Id, MatchSeats.People, held, decision, TestContext.Current.CancellationToken);
            step.IsSuccess.ShouldBeTrue($"{decision.Slot} {decision.Decision.Kind}: {(step.IsFailure ? step.Error.Code : string.Empty)}");
            step.Value.Decided.ShouldBe([decision]);
            held = step.Value.Held;
            last = step.Value;
        }

        last.ShouldNotBeNull().Outcome.ShouldBe(played.Outcome);
        Trace(log).ShouldBe(played.Events);
    }

    [Fact]
    public async Task A_pick_is_held_and_shown_to_its_seat_alone_until_both_seats_are_done()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var log = Logged(store);
        var stepper = Stepper(store);
        var begun = (await stepper.AdvanceAsync(match.Id, MatchSeats.People, HeldAnswers.None, TestContext.Current.CancellationToken)).Value;
        begun.Asked.ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
        var offered = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, begun.Held, TestContext.Current.CancellationToken)).Value.Options.Evolution.ShouldNotBeNull();
        offered.RemainingPicks.ShouldBe(2);
        var pick = PlayerDecision.Buy(offered.Creatures[0].Creature, offered.Creatures[0].AvailableTiers[0]);

        var picked = (await stepper.SubmitAsync(match.Id, MatchSeats.People, begun.Held, new SeatDecision(PlayerSlot.Player1, pick), TestContext.Current.CancellationToken)).Value;

        picked.Asked.ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
        picked.Held.Player1.ShouldBe([pick]);
        log.All.ShouldBeEmpty();
        var mine = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, picked.Held, TestContext.Current.CancellationToken)).Value;
        mine.Board.EvolutionChoices.ShouldHaveSingleItem().Creature.ShouldBe(pick.Creature!.Value);
        mine.Options.Evolution.ShouldNotBeNull().RemainingPicks.ShouldBe(1);
        mine.Options.Evolution!.Creatures.ShouldNotContain(option => option.Creature == pick.Creature);
        var theirs = (await stepper.SeatAsync(match.Id, PlayerSlot.Player2, picked.Held, TestContext.Current.CancellationToken)).Value;
        theirs.Options.Evolution.ShouldNotBeNull().RemainingPicks.ShouldBe(2);

        var passed = (await stepper.SubmitAsync(match.Id, MatchSeats.People, picked.Held, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value;

        passed.Asked.ShouldBe([PlayerSlot.Player2]);
        log.All.ShouldBeEmpty();
        var done = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, passed.Held, TestContext.Current.CancellationToken)).Value;
        done.Asked.ShouldBeNull();
        done.Options.Kind.ShouldBe(PlayerOptionsKind.Waiting);

        var revealed = (await stepper.SubmitAsync(match.Id, MatchSeats.People, passed.Held, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value;

        revealed.Held.IsEmpty.ShouldBeTrue();
        log.All.OfType<EvolutionChoiceSubmitted>().ShouldHaveSingleItem().Slot.ShouldBe(PlayerSlot.Player1);
        log.All.OfType<EvolutionPassed>().Select(pass => pass.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
    }

    [Fact]
    public async Task A_Player_agent_answers_at_once_and_its_answers_wait_for_the_person_s()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var log = Logged(store);
        var seats = new MatchSeats(null, Passing());

        var step = (await Stepper(store).AdvanceAsync(match.Id, seats, HeldAnswers.None, TestContext.Current.CancellationToken)).Value;

        step.Asked.ShouldBe([PlayerSlot.Player1]);
        step.Decided.ShouldBe([new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass)]);
        step.Held.Player2.ShouldBe([PlayerDecision.Pass]);
        log.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Player_agent_seated_over_a_person_s_held_answers_finishes_them()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var stepper = Stepper(store);
        var offered = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, HeldAnswers.None, TestContext.Current.CancellationToken)).Value.Options.Evolution.ShouldNotBeNull();
        var pick = PlayerDecision.Buy(offered.Creatures[0].Creature, offered.Creatures[0].AvailableTiers[0]);
        var picked = (await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, pick), TestContext.Current.CancellationToken)).Value;

        var step = (await stepper.AdvanceAsync(match.Id, new MatchSeats(Passing(), null), picked.Held, TestContext.Current.CancellationToken)).Value;

        step.Held.Player1.ShouldBe([pick, PlayerDecision.Pass]);
        step.Asked.ShouldBe([PlayerSlot.Player2]);
    }

    [Fact]
    public async Task A_decision_the_seat_is_not_being_asked_is_late_and_changes_nothing()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var log = Logged(store);
        var stepper = Stepper(store);
        var creature = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, HeldAnswers.None, TestContext.Current.CancellationToken)).Value.Board.Allies[0].Id;

        var speed = await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(creature, Speed.Quick)), TestContext.Current.CancellationToken);
        var passed = (await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value;
        var again = await stepper.SubmitAsync(match.Id, MatchSeats.People, passed.Held, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken);

        speed.Error.ShouldBe(DecisionErrors.NotPending);
        again.Error.ShouldBe(DecisionErrors.NotPending);
        log.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Speeds_are_asked_a_creature_at_a_time_in_the_order_the_options_list_them()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var stepper = Stepper(store);
        var held = (await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value.Held;
        held = (await stepper.SubmitAsync(match.Id, MatchSeats.People, held, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value.Held;
        var view = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, held, TestContext.Current.CancellationToken)).Value;
        var missing = view.Options.Speed.ShouldNotBeNull().Missing;
        view.Asked.ShouldBe(new SeatQuestion(PlayerOptionsKind.Speed, missing[0]));

        var second = await stepper.SubmitAsync(match.Id, MatchSeats.People, held, new SeatDecision(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(missing[1], Speed.Quick)), TestContext.Current.CancellationToken);
        var first = (await stepper.SubmitAsync(match.Id, MatchSeats.People, held, new SeatDecision(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(missing[0], Speed.Quick)), TestContext.Current.CancellationToken)).Value;

        second.Error.ShouldBe(DecisionErrors.NotPending);
        var next = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, first.Held, TestContext.Current.CancellationToken)).Value;
        next.Asked.ShouldBe(new SeatQuestion(PlayerOptionsKind.Speed, missing[1]));
        next.Board.SpeedChoices.ShouldHaveSingleItem().Creature.ShouldBe(missing[0]);
    }

    [Fact]
    public async Task A_decision_the_options_do_not_offer_is_refused()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var stepper = Stepper(store);
        var creature = (await stepper.SeatAsync(match.Id, PlayerSlot.Player1, HeldAnswers.None, TestContext.Current.CancellationToken)).Value.Board.Allies[0].Id;
        var unknown = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Buy(creature, TierId.Parse("tier:nothing:v1")));

        var step = await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, unknown, TestContext.Current.CancellationToken);

        step.Error.ShouldBe(DecisionErrors.TierNotOffered);
    }

    [Fact]
    public async Task A_seat_a_Player_agent_holds_takes_no_decision_from_outside()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var seats = new MatchSeats(Passing(), null);

        await Should.ThrowAsync<ArgumentException>(() =>
            Stepper(store).SubmitAsync(match.Id, seats, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_concession_ends_the_step_and_lets_go_of_the_answers_held()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(), new TestRandom(3));
        var stepper = Stepper(store);
        var held = (await stepper.SubmitAsync(match.Id, MatchSeats.People, HeldAnswers.None, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), TestContext.Current.CancellationToken)).Value.Held;
        held.IsEmpty.ShouldBeFalse();
        match.Concede(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();

        var step = (await stepper.AdvanceAsync(match.Id, MatchSeats.People, held, TestContext.Current.CancellationToken)).Value;
        var late = await stepper.SubmitAsync(match.Id, MatchSeats.People, held, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), TestContext.Current.CancellationToken);

        step.Outcome.ShouldBe(match.Outcome);
        step.Outcome.ShouldNotBeNull().Winner.ShouldBe(PlayerSlot.Player1);
        step.Held.IsEmpty.ShouldBeTrue();
        step.Asked.ShouldBeEmpty();
        late.Error.ShouldBe(DecisionErrors.NotPending);
    }

    [Fact]
    public async Task A_match_that_does_not_exist_is_a_refusal()
    {
        var step = await Stepper(new MatchStore()).AdvanceAsync(MatchId.New(), MatchSeats.People, HeldAnswers.None, TestContext.Current.CancellationToken);

        step.IsFailure.ShouldBeTrue();
    }

    private static RuleSet Rules(CombatDeclaration declaration) => MatchStore.TwoOnTwo().WithCombatDeclaration(declaration);

    private static RandomAgent Agent(uint seed, PlayerSlot slot) => new RandomAgent(new TestRandom(seed + (slot == PlayerSlot.Player1 ? 1u : 2u)));

    private static async Task<(MatchOutcome? Outcome, IReadOnlyList<string> Events)> DrivenAsync(uint seed, CombatDeclaration declaration)
    {
        var store = new MatchStore();
        var match = store.Started(Rules(declaration), new TestRandom(seed));
        var log = Logged(store);
        var driver = new MatchDriver(Commands(store), Queries(store)) { AsksSpeedsAndIntentsTogether = true };

        var outcome = await driver.PlayAsync(match.Id, Agent(seed, PlayerSlot.Player1), Agent(seed, PlayerSlot.Player2), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        return (outcome.Value, Trace(log));
    }

    /// <summary>
    /// A match stepped to its end. A seat marked as a person's is answered from outside, by the agent that would
    /// sit there, one question at a time through the seat's view; and at every stop, the seats the step says are
    /// asked are the seats whose view says so.
    /// </summary>
    private static async Task<(MatchOutcome? Outcome, IReadOnlyList<string> Events, IReadOnlyList<SeatDecision> Decided)> SteppedAsync(uint seed, CombatDeclaration declaration, bool person1, bool person2)
    {
        var store = new MatchStore();
        var match = store.Started(Rules(declaration), new TestRandom(seed));
        var log = Logged(store);
        var stepper = Stepper(store);
        var agents = new Dictionary<PlayerSlot, IPlayerAgent> { [PlayerSlot.Player1] = Agent(seed, PlayerSlot.Player1), [PlayerSlot.Player2] = Agent(seed, PlayerSlot.Player2) };
        var seats = new MatchSeats(person1 ? null : agents[PlayerSlot.Player1], person2 ? null : agents[PlayerSlot.Player2]);
        var decided = new List<SeatDecision>();

        var step = (await stepper.AdvanceAsync(match.Id, seats, HeldAnswers.None, TestContext.Current.CancellationToken)).Value;
        for (var taken = 0; !step.IsOver; taken++)
        {
            taken.ShouldBeLessThan(5000);
            decided.AddRange(step.Decided);
            foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
            {
                var seat = (await stepper.SeatAsync(match.Id, slot, step.Held, TestContext.Current.CancellationToken)).Value;
                (seat.Asked is not null).ShouldBe(step.Asked.Contains(slot));
            }

            var asked = step.Asked[0];
            seats.Of(asked).ShouldBeNull();
            var view = (await stepper.SeatAsync(match.Id, asked, step.Held, TestContext.Current.CancellationToken)).Value;
            var answer = new SeatDecision(asked, Answer(agents[asked], view));
            var next = await stepper.SubmitAsync(match.Id, seats, step.Held, answer, TestContext.Current.CancellationToken);
            next.IsSuccess.ShouldBeTrue($"{asked} {answer.Decision.Kind}: {(next.IsFailure ? next.Error.Code : string.Empty)}");
            step = next.Value;
        }

        decided.AddRange(step.Decided);
        step.Held.IsEmpty.ShouldBeTrue();
        step.Asked.ShouldBeEmpty();
        return (step.Outcome, Trace(log), decided);
    }

    /// <summary>What an agent answers to the question a seat's view asks, on the board and the options the view shows.</summary>
    private static PlayerDecision Answer(IPlayerAgent agent, SeatView view)
    {
        var asked = view.Asked.ShouldNotBeNull();
        switch (asked.Kind)
        {
            case PlayerOptionsKind.Evolution:
                return agent.DecideEvolution(view.Board, view.Options.Evolution!).Choice is { } choice ? PlayerDecision.Buy(choice.Creature, choice.Tier) : PlayerDecision.Pass;
            case PlayerOptionsKind.Speed:
                return PlayerDecision.ChooseSpeed(asked.Creature!.Value, agent.DecideSpeed(view.Board, asked.Creature.Value));
            case PlayerOptionsKind.TieOrder:
                return PlayerDecision.OrderTies(agent.DecideTieOrder(view.Board, view.Options.TieOrder!));
            case PlayerOptionsKind.Intent:
                var option = view.Options.Intent!.Creatures.Single(offered => offered.Creature == asked.Creature);
                return PlayerDecision.DeclareIntent(option.Creature, agent.DecideIntent(view.Board, option));
            case PlayerOptionsKind.Target:
                return PlayerDecision.BindTargets(agent.DecideTargets(view.Board, view.Options.Target!));
            case PlayerOptionsKind.Action:
                var action = view.Options.Action!;
                var chosen = agent.DecideAction(view.Board, action);
                return PlayerDecision.Act(action.Actor, chosen.Spell, chosen.Targets);
            default:
                throw new InvalidOperationException($"A seat is not asked {asked.Kind}.");
        }
    }

    /// <summary>An agent that buys nothing: all a test of the picks needs from the other seat.</summary>
    private static IPlayerAgent Passing()
    {
        var agent = Substitute.For<IPlayerAgent>();
        agent.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        return agent;
    }

    private static MatchStepper Stepper(MatchStore store) => new(Commands(store), Queries(store));

    private static MatchCommandHandlers Commands(MatchStore store) =>
        new(
            new SubmitEvolutionChoiceHandler(store.Workflow),
            new PassEvolutionHandler(store.Workflow),
            new SubmitSpeedChoiceHandler(store.Workflow),
            new SubmitTieOrderHandler(store.Workflow),
            new SubmitIntentHandler(store.Workflow),
            new SubmitActionHandler(store.Workflow));

    private static MatchQueryHandlers Queries(MatchStore store) =>
        new(
            new GetBoardStateForPlayerHandler(store.Workflow),
            new GetPlayerOptionsHandler(store.Workflow, TestContent.Resources));

    private static EventLog Logged(MatchStore store)
    {
        var log = new EventLog();
        store.Dispatcher.DispatchAsync(Arg.Any<DownfallArena.Domain.Matches.Match>(), Arg.Any<CancellationToken>()).Returns(call => log.RecordAsync(call.Arg<DownfallArena.Domain.Matches.Match>()));
        return log;
    }

    /// <summary>
    /// The events as text, one a line, with the ids a match draws for itself blanked: two matches from the same
    /// seed differ in their own id and their rounds', and in nothing a player could see.
    /// </summary>
    private static List<string> Trace(EventLog log) =>
        [.. log.All.Select(raised => $"{raised.GetType().Name} {DrawnId().Replace(JsonSerializer.Serialize(raised, raised.GetType(), EventJson), "id")}")];

    private static readonly JsonSerializerOptions EventJson = new() { Converters = { new CreatureKey() } };

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
    private static partial Regex DrawnId();

    /// <summary>Writes a creature id where JSON wants text: some events key what they carry by creature.</summary>
    private sealed class CreatureKey : JsonConverter<CreatureId>
    {
        public override CreatureId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WritePropertyName(value.ToString());
    }

    /// <summary>Every event the match raised, in the order it raised them.</summary>
    private sealed class EventLog
    {
        private readonly List<IDomainEvent> _all = [];

        public IReadOnlyList<IDomainEvent> All => _all;

        public Task RecordAsync(DownfallArena.Domain.Matches.Match match)
        {
            _all.AddRange(match.DomainEvents);
            match.ClearDomainEvents();
            return Task.CompletedTask;
        }
    }
}
