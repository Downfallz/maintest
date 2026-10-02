using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.Agents;

public sealed class RoundRolloutTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();

    /// <summary>
    /// The test ADR 0094 asks for, so the round the rollout walks cannot drift from the one the match runs: a
    /// match played to its end by the greedy agent in both seats, and the same match rolled out from its first
    /// Speed sub-phase on dice that roll what the match's rolled. Every decision, every roll and every action
    /// has to land where the match's did for the two to end alike and to score their actions alike, across
    /// seeds that reach purchases, ties, criticals, stuns, bleeds and both kinds of ending.
    /// </summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(5u)]
    [InlineData(6u)]
    [InlineData(7u)]
    [InlineData(8u)]
    public async Task A_rollout_plays_the_match_the_match_plays(uint seed)
    {
        var store = new MatchStore();
        var events = new Collected();
        var match = store.Started(Rules, new TestRandom(seed));
        var origin = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var start = match.Snapshots();
        var greedy = new GreedyAgent(TestContent.Resources, Rules);
        var scorer = new ActionScorer(TestContent.Resources, Rules, ScoringWeights.Default);

        var played = await Driver(store.WorkflowWith(events)).PlayAsync(match.Id, greedy, greedy, TestContext.Current.CancellationToken);

        var opening = events.Of<PurchasesRevealed>().First().Choices;
        var rollout = new RoundRollout(greedy, scorer, TestContent.Resources, Rules);
        var value = rollout.Play(origin, Advance.Buy(start, opening, TestContent.Resources), Rules.RoundCap, new TestRandom(seed));

        value.ShouldBe(RolloutValue.Of(played.Value, PlayerSlot.Player1, Scored(events, scorer)));
    }

    [Fact]
    public void A_rollout_stops_at_the_rounds_it_was_given()
    {
        var store = new MatchStore();
        var match = store.Started(Rules, new TestRandom(1));
        var origin = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var greedy = new GreedyAgent(TestContent.Resources, Rules);
        var rollout = new RoundRollout(greedy, new ActionScorer(TestContent.Resources, Rules, ScoringWeights.Default), TestContent.Resources, Rules);

        var oneRound = rollout.Play(origin, match.Snapshots(), 1, new TestRandom(1));
        var whole = rollout.Play(origin, match.Snapshots(), Rules.RoundCap, new TestRandom(1));

        oneRound.Outcome.ShouldBe(0, "two full teams do not end in a round");
        whole.Outcome.ShouldNotBe(0);
    }

    [Fact]
    public void A_seat_picks_one_package_per_creature_up_to_its_picks()
    {
        var store = new MatchStore();
        var match = store.Started(Rules, new TestRandom(1));
        var greedy = new GreedyAgent(TestContent.Resources, Rules);
        var rollout = new RoundRollout(greedy, new ActionScorer(TestContent.Resources, Rules, ScoringWeights.Default), TestContent.Resources, Rules);

        var picks = rollout.Picks(PlayerBoardStateProjection.Build(match, PlayerSlot.Player2), match.Snapshots(), 1, PlayerSlot.Player2, []);

        picks.Count.ShouldBe(Rules.EvolutionPicksIn(1));
        picks.Select(pick => pick.Creature).ShouldBeUnique();
        picks.ShouldAllBe(pick => match.Snapshots().Single(creature => creature.Id == pick.Creature).Owner == PlayerSlot.Player2);
    }

    /// <summary>What the rollout sums, read off the match: each action scored on the board it landed on, the first seat's for and the other's against.</summary>
    private static double Scored(Collected events, ActionScorer scorer) =>
        events.Of<CombatActionResolved>().Sum(resolved =>
        {
            var frame = resolved.Frame.ShouldNotBeNull();
            var actor = resolved.Resolution.Action.Actor;
            var index = frame.Timeline.ToList().FindIndex(slot => slot.Creature == actor);
            var stillToAct = frame.Timeline.Skip(index + 1).Select(slot => slot.Creature).ToHashSet();
            var sign = frame.Before.First(creature => creature.Id == actor).Owner == PlayerSlot.Player1 ? 1 : -1;
            return sign * scorer.Score(resolved.Resolution, frame.Before, stillToAct: stillToAct);
        });

    private static MatchDriver Driver(MatchWorkflow workflow) =>
        new(
            new MatchCommandHandlers(
                new SubmitEvolutionChoiceHandler(workflow),
                new PassEvolutionHandler(workflow),
                new SubmitSpeedChoiceHandler(workflow),
                new SubmitTieOrderHandler(workflow),
                new SubmitIntentHandler(workflow),
                new SubmitActionHandler(workflow)),
            new MatchQueryHandlers(
                new GetBoardStateForPlayerHandler(workflow),
                new GetPlayerOptionsHandler(workflow, TestContent.Resources)));

    /// <summary>Every event the match raised, in order.</summary>
    private sealed class Collected : IDomainEventListener
    {
        private readonly List<IDomainEvent> _events = [];

        public Type EventType => typeof(IDomainEvent);

        public IEnumerable<T> Of<T>() => _events.OfType<T>();

        public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
