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
        var passer = Substitute.For<IPlayerAgent>();
        passer.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        passer.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(Speed.Standard);
        passer.DecideIntent(Arg.Any<PlayerBoardState>(), Arg.Any<IntentOption>()).Returns(TestContent.Strike);
        passer.DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Any<TargetOptions>()).Returns(call => [call.Arg<TargetOptions>().LegalTargets.Candidates[0]]);

        var outcome = await Driver(store).PlayAsync(match.Id, passer, passer, TestContext.Current.CancellationToken);

        outcome.Value.ShouldBe(new MatchOutcome(null, MatchEndReason.RoundCap));
        match.Creatures.ShouldAllBe(creature => creature.KnownSpells.Count == 1);
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
