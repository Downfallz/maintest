using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using NSubstitute;

namespace DownfallArena.Application.Tests.Learning.Tracing;

public sealed class MatchTraceRecorderTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo(roundCap: 6);
    private static readonly RunStamp Stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, Rules, FeatureSchema.Build(TestContent.Resources, Rules), "Random", "Random", 1);

    [Fact]
    public async Task Every_event_of_a_match_is_kept_with_both_boards_after_the_command_that_raised_it()
    {
        var store = new MatchStore();
        var tracer = new MatchTraceRecorder(store.Repository);
        var workflow = store.WorkflowWith(tracer);
        var factory = new TestRandomFactory();
        var matchId = (await new CreateMatchHandler(workflow, TestContent.Resources, factory).HandleAsync(new CreateMatch(Rules, 1), TestContext.Current.CancellationToken)).Value;
        var join = new JoinMatchHandler(workflow);
        await join.HandleAsync(new JoinMatch(matchId, MatchStore.Alice, MatchStore.Roster(Rules)), TestContext.Current.CancellationToken);
        await join.HandleAsync(new JoinMatch(matchId, MatchStore.Bob, MatchStore.Roster(Rules)), TestContext.Current.CancellationToken);

        var afterJoin = tracer.EntriesOf(matchId);
        afterJoin[0].Event.ShouldBeOfType<PlayerJoined>();
        afterJoin.Select(entry => entry.Event.GetType()).ShouldContain(typeof(MatchStarted));
        afterJoin.Select(entry => entry.Event.GetType()).ShouldContain(typeof(RoundStarted));
        afterJoin[0].Player1.State.ShouldBe(MatchState.WaitingForPlayers);
        afterJoin[^1].Player1.State.ShouldBe(MatchState.InProgress);
        afterJoin[^1].Round.ShouldBe(1);

        var outcome = await Handlers.Driver(workflow).PlayAsync(matchId, new RandomAgent(new TestRandom(1)), new RandomAgent(new TestRandom(2)), TestContext.Current.CancellationToken);
        var match = (await store.Repository.FindAsync(matchId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var entries = tracer.EntriesOf(matchId);

        entries.Select(entry => entry.Sequence).ShouldBe(Enumerable.Range(0, entries.Count));
        entries.ShouldAllBe(entry => entry.Event.MatchId == matchId);
        entries.ShouldAllBe(entry => entry.Player1.Slot == PlayerSlot.Player1 && entry.Player2.Slot == PlayerSlot.Player2);
        entries[^1].Event.ShouldBeOfType<MatchEnded>().Outcome.ShouldBe(outcome.Value);
        var final = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        entries[^1].Player1.State.ShouldBe(final.State);
        entries[^1].Player1.RoundNumber.ShouldBe(final.RoundNumber);
        entries[^1].Player1.SubPhase.ShouldBe(final.SubPhase);
        entries[^1].Player1.Outcome.ShouldBe(final.Outcome);
        entries[^1].Round.ShouldBe(final.RoundNumber);
        entries[^1].Player1.Allies.Select(creature => creature.Health).ShouldBe(match.Snapshots().Where(creature => creature.Owner == PlayerSlot.Player1).Select(creature => creature.Health));
        entries.Count(entry => entry.Event is RoundStarted).ShouldBe(match.CurrentRound.ShouldNotBeNull().Number);

        var trace = tracer.Complete(matchId, Stamp, 1);

        trace.MatchId.ShouldBe(matchId);
        trace.Seed.ShouldBe(1);
        trace.Stamp.ShouldBe(Stamp);
        trace.Entries.Count.ShouldBe(entries.Count);
        trace.Outcome.ShouldBe(outcome.Value);
        tracer.EntriesOf(matchId).ShouldBeEmpty();
        tracer.Complete(matchId, Stamp, null).Entries.ShouldBeEmpty();
        tracer.Complete(matchId, Stamp, null).Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task Events_that_are_not_match_events_are_ignored()
    {
        var store = new MatchStore();
        var tracer = new MatchTraceRecorder(store.Repository);

        tracer.EventType.ShouldBe(typeof(IMatchEvent));
        await tracer.HandleAsync(new Unrelated(), TestContext.Current.CancellationToken);

        await store.Repository.DidNotReceive().FindAsync(Arg.Any<MatchId>(), Arg.Any<CancellationToken>());
        tracer.EntriesOf(MatchId.New()).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_event_of_a_match_that_is_not_stored_is_an_error()
    {
        var tracer = new MatchTraceRecorder(new MatchStore().Repository);

        await Should.ThrowAsync<InvalidOperationException>(() => tracer.HandleAsync(new RoundStarted(MatchId.New(), RoundId.First), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => tracer.HandleAsync(null!, TestContext.Current.CancellationToken));
        Should.Throw<ArgumentNullException>(() => tracer.Complete(MatchId.New(), null!, null));
    }

    private sealed record Unrelated : IDomainEvent;
}
