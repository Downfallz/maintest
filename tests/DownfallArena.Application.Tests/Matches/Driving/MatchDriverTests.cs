using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
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

public sealed class MatchDriverTests
{
    [Fact]
    public async Task Two_random_agents_play_a_match_to_its_end()
    {
        var store = new MatchStore();
        var match = store.Started(random: new TestRandom(42));
        var driver = Driver(store);

        var outcome = await driver.PlayAsync(match.Id, new RandomAgent(new TestRandom(1)), new RandomAgent(new TestRandom(2)), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        match.State.ShouldBe(MatchState.Ended);
        outcome.Value.ShouldBe(match.Outcome);
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task The_same_seeds_replay_the_same_match()
    {
        var first = await PlayAsync(seed: 42);
        var second = await PlayAsync(seed: 42);
        var other = await PlayAsync(seed: 43);

        first.Outcome.ShouldBe(second.Outcome);
        first.Rounds.ShouldBe(second.Rounds);
        first.Resolutions.ShouldBe(second.Resolutions);
        (other.Rounds != first.Rounds || other.Resolutions != first.Resolutions || other.Outcome != first.Outcome).ShouldBeTrue();
    }

    /// <summary>
    /// The two package picks are made face down and revealed together (ADR 0089), so neither has to wait for
    /// the other: both seats are asked at once (ADR 0092). Each agent here answers only once the other has been
    /// asked, which a driver asking in turn can never satisfy.
    /// </summary>
    [Fact]
    public async Task Both_players_are_asked_their_package_pick_at_the_same_time()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        using var bothAsked = new Barrier(2);
        var first = new Together(Scripted(TestContent.Strike), bothAsked);
        var second = new Together(Scripted(TestContent.Strike), bothAsked);

        var outcome = await Driver(store).PlayAsync(match.Id, first, second, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        first.AskedTogether.ShouldBeTrue();
        second.AskedTogether.ShouldBeTrue();
    }

    /// <summary>
    /// Speeds are chosen face down and so are intents, so a table asks both seats at once, as it asks their picks
    /// (ADR 0111). Each agent here answers each speed, or each intent, only once the other seat has been asked
    /// its own.
    /// </summary>
    [Theory]
    [InlineData(PlayerOptionsKind.Speed)]
    [InlineData(PlayerOptionsKind.Intent)]
    public async Task A_table_asks_both_players_their_speeds_and_their_intents_at_the_same_time(PlayerOptionsKind kind)
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        using var bothAsked = new Barrier(2);
        var first = new Together(Scripted(TestContent.Strike), bothAsked, kind);
        var second = new Together(Scripted(TestContent.Strike), bothAsked, kind);

        var outcome = await Driver(store, together: true).PlayAsync(match.Id, first, second, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        first.AskedTogether.ShouldBeTrue();
        second.AskedTogether.ShouldBeTrue();
    }

    /// <summary>
    /// A simulation asks speeds seat by seat, as it always has: bots answer at once, and its events and recorded
    /// steps keep the order the seed gives them. The barrier is never met, so each agent gives up waiting.
    /// </summary>
    [Fact]
    public async Task A_simulation_asks_speeds_seat_by_seat()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        using var bothAsked = new Barrier(2);
        var first = new Together(Scripted(TestContent.Strike), bothAsked, PlayerOptionsKind.Speed, patience: TimeSpan.FromMilliseconds(200));
        var second = new Together(Scripted(TestContent.Strike), bothAsked, PlayerOptionsKind.Speed, patience: TimeSpan.FromMilliseconds(200));

        var outcome = await Driver(store).PlayAsync(match.Id, first, second, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        first.AskedTogether.ShouldBeFalse();
    }

    /// <summary>
    /// At a table each creature's intent is declared on the board its owner would have read after declaring the
    /// ones before it (ADR 0039), though nothing reaches the match until both seats are done (ADR 0111).
    /// </summary>
    [Fact]
    public async Task At_a_table_each_intent_is_declared_on_a_board_that_carries_the_seat_s_earlier_ones()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        var first = new Declaring(Scripted(TestContent.Strike));
        var second = new Declaring(Scripted(TestContent.Strike));

        (await Driver(store, together: true).PlayAsync(match.Id, first, second, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        first.Seen.ShouldBe([0, 1]);
        second.Seen.ShouldBe([0, 1]);
    }

    /// <summary>
    /// Asked at once, answered in seat order: the engine is handed every speed and every intent of seat 1 before
    /// any of seat 2's, whichever seat finished first, so a rebuilt table replays its commands in the order they
    /// were recorded in.
    /// </summary>
    [Fact]
    public async Task At_a_table_speeds_and_intents_asked_at_once_are_submitted_in_seat_order()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 3), new TestRandom(5));
        var events = new EventLog();
        store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(call => events.RecordAsync(call.Arg<Match>()));

        using var answered = new SemaphoreSlim(0);

        (await Driver(store, together: true).PlayAsync(match.Id, new Behind(new RandomAgent(new TestRandom(1)), answered), new Ahead(new RandomAgent(new TestRandom(2)), answered), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        foreach (var round in events.All.OfType<IMatchEvent>().GroupBy(Round))
        {
            var speeds = round.OfType<SpeedChoiceSubmitted>().Select(submitted => submitted.Slot).ToList();
            var intents = round.OfType<IntentSubmitted>().Select(submitted => submitted.Slot).ToList();
            speeds.ShouldBe([.. speeds.OrderBy(slot => slot)], $"round {round.Key}");
            intents.ShouldBe([.. intents.OrderBy(slot => slot)], $"round {round.Key}");
        }
    }

    private static RoundId? Round(IMatchEvent matchEvent) => matchEvent switch
    {
        SpeedChoiceSubmitted speed => speed.RoundId,
        IntentSubmitted intent => intent.RoundId,
        _ => null,
    };

    /// <summary>
    /// What lets a seat at a table declare all its intents before anything is submitted: an intent changes its
    /// owner's board by that intent and nothing else, and the other seat's board not at all, so the board the
    /// driver builds for the next creature is the one the match would have served (ADR 0039, ADR 0111).
    /// </summary>
    [Fact]
    public async Task An_intent_changes_its_owner_s_board_by_that_intent_alone_and_the_other_s_not_at_all()
    {
        var store = new MatchStore();
        var match = store.Started(RuleSet.Create(1, 2, 2, 30, 2.0), new TestRandom(3));
        var commands = Commands(store);
        var queries = Queries(store);
        (await commands.PassEvolution.HandleAsync(new PassEvolution(match.Id, PlayerSlot.Player1), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await commands.PassEvolution.HandleAsync(new PassEvolution(match.Id, PlayerSlot.Player2), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var mine = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player1), TestContext.Current.CancellationToken)).Value;
        var theirs = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player2), TestContext.Current.CancellationToken)).Value;
        (await commands.SubmitSpeedChoice.HandleAsync(new SubmitSpeedChoice(match.Id, PlayerSlot.Player1, mine.Allies[0].Id, Speed.Quick), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await commands.SubmitSpeedChoice.HandleAsync(new SubmitSpeedChoice(match.Id, PlayerSlot.Player2, theirs.Allies[0].Id, Speed.Standard), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var before = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player1), TestContext.Current.CancellationToken)).Value;
        var otherBefore = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player2), TestContext.Current.CancellationToken)).Value;
        before.SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        var intent = new CombatIntent(before.Allies[0].Id, TestContent.Strike);

        (await commands.SubmitIntent.HandleAsync(new SubmitIntent(match.Id, PlayerSlot.Player1, intent.Actor, intent.Spell), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var after = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player1), TestContext.Current.CancellationToken)).Value;
        var otherAfter = (await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player2), TestContext.Current.CancellationToken)).Value;
        JsonSerializer.Serialize(after).ShouldBe(JsonSerializer.Serialize(before with { Intents = [.. before.Intents, intent] }));
        JsonSerializer.Serialize(otherAfter).ShouldBe(JsonSerializer.Serialize(otherBefore));
    }

    /// <summary>An agent whose every answer of one kind waits until the other seat has been asked its own.</summary>
    private sealed class Together(IPlayerAgent inner, Barrier bothAsked, PlayerOptionsKind kind = PlayerOptionsKind.Evolution, TimeSpan? patience = null) : IPlayerAgent
    {
        public bool AskedTogether { get; private set; } = true;

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
        {
            Meet(PlayerOptionsKind.Evolution);
            return inner.DecideEvolution(board, options);
        }

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            Meet(PlayerOptionsKind.Speed);
            return inner.DecideSpeed(board, creature);
        }

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => inner.DecideTieOrder(board, options);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            Meet(PlayerOptionsKind.Intent);
            return inner.DecideIntent(board, intentOption);
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);

        private void Meet(PlayerOptionsKind asked)
        {
            if (asked == kind)
            {
                AskedTogether &= bothAsked.SignalAndWait(patience ?? TimeSpan.FromSeconds(5));
            }
        }
    }

    /// <summary>An agent that notes how many intents of its own each intent it declares is shown.</summary>
    private sealed class Declaring(IPlayerAgent inner) : IPlayerAgent
    {
        public List<int> Seen { get; } = [];

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => inner.DecideSpeed(board, creature);

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => inner.DecideTieOrder(board, options);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            Seen.Add(board.Intents.Count);
            return inner.DecideIntent(board, intentOption);
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }

    /// <summary>
    /// Seat 1's agent, answering each speed and intent only once seat 2 has answered one: what is then submitted
    /// first is the seat order, not the order the answers came in. A seat with fewer creatures standing gives up
    /// waiting rather than hang.
    /// </summary>
    private sealed class Behind(IPlayerAgent inner, SemaphoreSlim answered) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            answered.Wait(TimeSpan.FromMilliseconds(500));
            return inner.DecideSpeed(board, creature);
        }

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => inner.DecideTieOrder(board, options);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            answered.Wait(TimeSpan.FromMilliseconds(500));
            return inner.DecideIntent(board, intentOption);
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }

    /// <summary>Seat 2's agent, saying so each time it has answered a speed or an intent.</summary>
    private sealed class Ahead(IPlayerAgent inner, SemaphoreSlim answered) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            var speed = inner.DecideSpeed(board, creature);
            answered.Release();
            return speed;
        }

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => inner.DecideTieOrder(board, options);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            var spell = inner.DecideIntent(board, intentOption);
            answered.Release();
            return spell;
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }

    /// <summary>
    /// ADR 0110. A rule set that declares no intent is played to its end by the driver alone: every slot asks its
    /// owner for a spell and its targets at once, and no intent is ever submitted.
    /// </summary>
    [Fact]
    public async Task Under_activation_declaration_a_greedy_and_a_random_agent_play_a_match_to_its_end_without_an_intent()
    {
        var store = new MatchStore();
        var rules = MatchStore.TwoOnTwo().WithCombatDeclaration(CombatDeclaration.AtActivation);
        var match = store.Started(rules, new TestRandom(42));
        var events = new EventLog();
        store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(call => events.RecordAsync(call.Arg<Match>()));

        var outcome = await Driver(store).PlayAsync(match.Id, new GreedyAgent(TestContent.Resources, rules), new RandomAgent(new TestRandom(1)), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        match.State.ShouldBe(MatchState.Ended);
        outcome.Value.ShouldBe(match.Outcome);
        events.All.OfType<IntentSubmitted>().ShouldBeEmpty();
        events.All.OfType<ActionRevealed>().ShouldNotBeEmpty();
    }

    /// <summary>
    /// ADR 0110. Under that rule the driver asks for an action at each slot and submits what it is given; it never
    /// asks for an intent, nor for targets on their own.
    /// </summary>
    [Fact]
    public async Task Under_activation_declaration_each_slot_is_asked_for_an_action_and_never_for_an_intent()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1).WithCombatDeclaration(CombatDeclaration.AtActivation), new TestRandom(1));
        var agent = Scripted(TestContent.Strike);
        agent.DecideAction(Arg.Any<PlayerBoardState>(), Arg.Any<ActionOptions>())
            .Returns(call => new ChosenAction(call.Arg<ActionOptions>().Spells[0].Spell, FreshTarget(call.Arg<PlayerBoardState>(), call.Arg<ActionOptions>().Spells[0])));

        var outcome = await Driver(store).PlayAsync(match.Id, agent, agent, TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        agent.Received(4).DecideAction(Arg.Any<PlayerBoardState>(), Arg.Any<ActionOptions>());
        agent.DidNotReceive().DecideIntent(Arg.Any<PlayerBoardState>(), Arg.Any<IntentOption>());
        agent.DidNotReceive().DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Any<TargetOptions>());
    }

    [Fact]
    public async Task A_pass_from_an_agent_is_submitted_as_a_pass()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        var passer = Scripted(TestContent.Strike);

        var outcome = await Driver(store).PlayAsync(match.Id, passer, passer, TestContext.Current.CancellationToken);

        outcome.Value.ShouldBe(new MatchOutcome(null, MatchEndReason.RoundCap));
        match.Creatures.ShouldAllBe(creature => creature.KnownSpells.Count == 1);
    }

    /// <summary>Every creature goes Standard and ties, so each seat is asked its tie order and the match takes it.</summary>
    [Fact]
    public async Task A_tie_order_is_asked_of_the_agent_and_submitted()
    {
        var store = new MatchStore();
        var match = store.Started(MatchStore.TwoOnTwo(roundCap: 1), new TestRandom(1));
        var agent = Scripted(TestContent.Strike);
        agent.DecideTieOrder(Arg.Any<PlayerBoardState>(), Arg.Any<TieOrderOptions>()).Returns(call => [.. call.Arg<TieOrderOptions>().AsRolled.Reverse()]);

        await Driver(store).PlayAsync(match.Id, agent, agent, TestContext.Current.CancellationToken);

        agent.Received(2).DecideTieOrder(Arg.Any<PlayerBoardState>(), Arg.Any<TieOrderOptions>());
        var round = match.CurrentRound.ShouldNotBeNull();
        round.TieOrderOf(PlayerSlot.Player1).ShouldNotBeNull().Count.ShouldBe(2);
        round.TieOrderOf(PlayerSlot.Player2).ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_team_the_upkeep_wipes_ends_the_match_before_anyone_is_asked_again()
    {
        var store = new MatchStore();
        var match = store.Empty(random: new TestRandom(1));
        match.Join(MatchStore.Alice, [TestContent.Bleeder, TestContent.Bleeder]).IsSuccess.ShouldBeTrue();
        match.Join(MatchStore.Bob, MatchStore.Roster(match.RuleSet)).IsSuccess.ShouldBeTrue();
        var bleeder = Scripted(TestContent.Rend);
        var striker = Scripted(TestContent.Strike);

        var outcome = await Driver(store).PlayAsync(match.Id, bleeder, striker, TestContext.Current.CancellationToken);

        // Round 1: the bleeders rend one enemy each. Round 2 starts by killing both enemies, and the match ends
        // there, at upkeep, before a single decision of round 2 is asked for (ADR 0083).
        outcome.Value.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        var round = match.CurrentRound.ShouldNotBeNull();
        round.Number.ShouldBe(2);
        round.SubPhase.ShouldBe(RoundSubPhase.OngoingEffects);
        bleeder.Received(2).DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Any<TargetOptions>());
        bleeder.DidNotReceive().DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Is<TargetOptions>(options => !options.LegalTargets.IsCastable));
    }

    [Fact]
    public async Task A_refused_decision_is_an_invariant_violation()
    {
        var store = new MatchStore();
        var match = store.Started();
        var cheater = Substitute.For<IPlayerAgent>();
        cheater.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>())
            .Returns(EvolutionDecision.Unlock(new EvolutionChoice(CreatureId.From(1), TestContent.SlamPack)));

        await Should.ThrowAsync<InvalidOperationException>(() => Driver(store).PlayAsync(match.Id, cheater, cheater, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A concession lands while the driver is inside a question (ADR 0087): the seat it was asking answers a
    /// match that has ended, and that answer is refused as such rather than as a bug. Here the agent concedes
    /// from inside its own speed choice, which is the tightest form of "while being asked".
    /// </summary>
    [Fact]
    public async Task A_concession_while_a_seat_is_being_asked_ends_the_match_with_its_outcome()
    {
        var store = new MatchStore();
        var match = store.Started();
        var quitter = Scripted(TestContent.Slam);
        quitter.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(_ =>
        {
            // Once: the driver asks the seat for its other creature's speed too, and that answer is to a match
            // that has already ended.
            if (match.State == MatchState.InProgress)
            {
                match.Concede(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
            }

            return Speed.Standard;
        });

        var outcome = await Driver(store).PlayAsync(match.Id, Scripted(TestContent.Slam), quitter, TestContext.Current.CancellationToken);

        outcome.Value.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Concession));
        match.State.ShouldBe(MatchState.Ended);
    }

    [Fact]
    public async Task A_match_that_is_not_in_progress_cannot_be_played()
    {
        var store = new MatchStore();
        var waiting = store.Empty();
        var agent = new RandomAgent(new TestRandom(1));

        (await Driver(store).PlayAsync(MatchId.New(), agent, agent, TestContext.Current.CancellationToken)).Error.ShouldBe(ApplicationErrors.MatchNotFound);
        (await Driver(store).PlayAsync(waiting.Id, agent, agent, TestContext.Current.CancellationToken)).Error.ShouldBe(MatchErrors.NotInProgress);
        await Should.ThrowAsync<ArgumentNullException>(() => Driver(store).PlayAsync(waiting.Id, null!, agent, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => Driver(store).PlayAsync(waiting.Id, agent, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An agent that passes, chooses Standard, keeps the order the roll-off left, declares the given spell, and hits the first legal target that no
    /// revealed action of the round targets yet (or the first one when every candidate is taken).
    /// </summary>
    private static IPlayerAgent Scripted(SpellId spell)
    {
        var agent = Substitute.For<IPlayerAgent>();
        agent.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        agent.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(Speed.Standard);
        agent.DecideTieOrder(Arg.Any<PlayerBoardState>(), Arg.Any<TieOrderOptions>()).Returns(call => call.Arg<TieOrderOptions>().AsRolled);
        agent.DecideIntent(Arg.Any<PlayerBoardState>(), Arg.Any<IntentOption>()).Returns(spell);
        agent.DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Any<TargetOptions>()).Returns(call => FreshTarget(call.Arg<PlayerBoardState>(), call.Arg<TargetOptions>()));
        return agent;
    }

    private static IReadOnlyList<CreatureId> FreshTarget(PlayerBoardState board, TargetOptions options)
    {
        var taken = board.RevealedActions.SelectMany(action => action.Targets).ToHashSet();
        var candidates = options.LegalTargets.Candidates;
        var fresh = candidates.Where(candidate => !taken.Contains(candidate)).ToList();
        return [.. (fresh.Count > 0 ? fresh : candidates).Take(1)];
    }

    private static MatchDriver Driver(MatchStore store, bool together = false) => new(Commands(store), Queries(store)) { AsksSpeedsAndIntentsTogether = together };

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

    private static async Task<(MatchOutcome? Outcome, int Rounds, int Resolutions)> PlayAsync(uint seed)
    {
        var store = new MatchStore();
        var match = store.Started(random: new TestRandom(seed));
        var recorder = new EventRecorder();
        store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(call => recorder.RecordAsync(call.Arg<Match>()));

        await Driver(store).PlayAsync(match.Id, new RandomAgent(new TestRandom(seed + 1)), new RandomAgent(new TestRandom(seed + 2)), TestContext.Current.CancellationToken);

        return (match.Outcome, recorder.Rounds, recorder.Resolutions);
    }

    /// <summary>Every event the match raised, in the order it raised them.</summary>
    private sealed class EventLog
    {
        private readonly List<IDomainEvent> _all = [];

        public IReadOnlyList<IDomainEvent> All => _all;

        public Task RecordAsync(Match match)
        {
            _all.AddRange(match.DomainEvents);
            match.ClearDomainEvents();
            return Task.CompletedTask;
        }
    }

    private sealed class EventRecorder
    {
        public int Rounds { get; private set; }

        public int Resolutions { get; private set; }

        public Task RecordAsync(Match match)
        {
            Rounds += match.DomainEvents.OfType<RoundStarted>().Count();
            Resolutions += match.DomainEvents.OfType<CombatActionResolved>().Count();
            match.ClearDomainEvents();
            return Task.CompletedTask;
        }
    }
}
