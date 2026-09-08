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

    [Fact]
    public async Task An_intent_left_without_a_legal_target_is_revealed_empty_and_the_match_still_ends()
    {
        var store = new MatchStore();
        var match = store.Empty(random: new TestRandom(1));
        match.Join(MatchStore.Alice, [TestContent.Bleeder, TestContent.Bleeder]).IsSuccess.ShouldBeTrue();
        match.Join(MatchStore.Bob, MatchStore.Roster(match.RuleSet)).IsSuccess.ShouldBeTrue();
        var bleeder = Scripted(TestContent.Rend);
        var striker = Scripted(TestContent.Strike);

        var outcome = await Driver(store).PlayAsync(match.Id, bleeder, striker, TestContext.Current.CancellationToken);

        // Round 1: the bleeders rend one enemy each. Round 2 starts by killing both enemies; the bleeders' intents
        // are revealed without targets and fizzle, and the round ends with Player2 defeated.
        outcome.Value.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(2);
        bleeder.Received(2).DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Is<TargetOptions>(options => !options.LegalTargets.IsCastable));
    }

    [Fact]
    public async Task A_refused_decision_is_an_invariant_violation()
    {
        var store = new MatchStore();
        var match = store.Started();
        var cheater = Substitute.For<IPlayerAgent>();
        cheater.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>())
            .Returns(EvolutionDecision.Unlock(new EvolutionChoice(CreatureId.From(1), TestContent.Slam)));

        await Should.ThrowAsync<InvalidOperationException>(() => Driver(store).PlayAsync(match.Id, cheater, cheater, TestContext.Current.CancellationToken));
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
    /// An agent that passes, chooses Standard, declares the given spell, and hits the first legal target that no
    /// revealed action of the round targets yet (or the first one when every candidate is taken).
    /// </summary>
    private static IPlayerAgent Scripted(SpellId spell)
    {
        var agent = Substitute.For<IPlayerAgent>();
        agent.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        agent.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(Speed.Standard);
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

    private static MatchDriver Driver(MatchStore store) =>
        new(
            new MatchCommandHandlers(
                new SubmitEvolutionChoiceHandler(store.Workflow),
                new PassEvolutionHandler(store.Workflow),
                new SubmitSpeedChoiceHandler(store.Workflow),
                new SubmitIntentHandler(store.Workflow),
                new SubmitActionHandler(store.Workflow),
                new ResolveNextActionHandler(store.Workflow)),
            new MatchQueryHandlers(
                new GetBoardStateForPlayerHandler(store.Workflow),
                new GetPlayerOptionsHandler(store.Workflow, TestContent.Resources)));

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
