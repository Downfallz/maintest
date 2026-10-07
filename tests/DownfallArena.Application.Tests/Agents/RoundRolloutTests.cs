using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.Agents;

public sealed class RoundRolloutTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();

    /// <summary>
    /// The test ADR 0094 asks for, so the round the rollout walks cannot drift from the one the match runs: a
    /// match played to its end by one agent in both seats, and the same match rolled out from its first Speed
    /// sub-phase on dice that roll what the match's rolled. Every decision, every roll and every action has to
    /// land where the match's did for the two to end alike and to score their actions alike, from either seat.
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
        await RollsAsTheMatchPlays(seed, Rules, MatchStore.Roster(Rules), Greedy(Rules), PlayerSlot.Player1);
        await RollsAsTheMatchPlays(seed, Rules, MatchStore.Roster(Rules), Greedy(Rules), PlayerSlot.Player2);
    }

    /// <summary>Bleeders, whose Rend can wipe a team at the upkeep, before anyone is asked anything (ADR 0083).</summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public async Task A_rollout_plays_the_upkeep_the_match_plays(uint seed) =>
        await RollsAsTheMatchPlays(seed, Rules, [TestContent.Bleeder, TestContent.Main], Greedy(Rules), PlayerSlot.Player1);

    /// <summary>A cap of two rounds, which no team of twenty health is wiped inside: the match ends on the cap.</summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public async Task A_rollout_plays_the_round_cap_the_match_plays(uint seed)
    {
        var rules = MatchStore.TwoOnTwo(roundCap: 2);
        await RollsAsTheMatchPlays(seed, rules, MatchStore.Roster(rules), Greedy(rules), PlayerSlot.Player1);
    }

    /// <summary>
    /// An agent that seats its tied creatures the other way round: the rollout asks it, as the match does. The
    /// two creatures of a side differ, so which of them acts first changes the match.
    /// </summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    public async Task A_rollout_asks_the_tie_order_the_match_asks(uint seed) =>
        await RollsAsTheMatchPlays(seed, Rules, [TestContent.Bleeder, TestContent.Main], new TiesReversed(Greedy(Rules)), PlayerSlot.Player1);

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

    /// <summary>
    /// A continuation picks the match up where a round's cleanup left it and plays the rounds from there,
    /// each from its start: it is how a combat move is read past its round. No rounds is nothing; one round
    /// cannot end two full teams; the cap's worth of rounds ends the match; and the score it is handed is
    /// carried into the sum.
    /// </summary>
    [Fact]
    public void A_continuation_plays_the_rounds_after_the_one_it_is_given()
    {
        var store = new MatchStore();
        var match = store.Started(Rules, new TestRandom(1));
        var origin = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var greedy = new GreedyAgent(TestContent.Resources, Rules);
        var rollout = new RoundRollout(greedy, new ActionScorer(TestContent.Resources, Rules, ScoringWeights.Default), TestContent.Resources, Rules);
        var cleaned = Advance.Cleanup(match.Snapshots(), TestContent.Resources);

        rollout.Continue(origin, cleaned, 2, 0, new TestRandom(1)).ShouldBe(new RolloutValue(0, 0));
        var oneRound = rollout.Continue(origin, cleaned, 2, 1, new TestRandom(1));
        oneRound.Outcome.ShouldBe(0, "two full teams do not end in a round");
        rollout.Continue(origin, cleaned, 2, Rules.RoundCap, new TestRandom(1)).Outcome.ShouldNotBe(0);
        rollout.Continue(origin, cleaned, 2, 1, new TestRandom(1), score: 5).Score.ShouldBe(oneRound.Score + 5, 1e-9);
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

    private static async Task RollsAsTheMatchPlays(uint seed, RuleSet rules, IReadOnlyList<CreatureDefinitionId> roster, IPlayerAgent agent, PlayerSlot seat)
    {
        var store = new MatchStore();
        var events = new Collected();
        var match = store.Empty(rules, new TestRandom(seed));
        match.Join(MatchStore.Alice, roster).IsSuccess.ShouldBeTrue();
        match.Join(MatchStore.Bob, roster).IsSuccess.ShouldBeTrue();
        match.ClearDomainEvents();
        var origin = PlayerBoardStateProjection.Build(match, seat);
        var start = match.Snapshots();
        var scorer = new ActionScorer(TestContent.Resources, rules, ScoringWeights.Default);

        var played = await Driver(store.WorkflowWith(events)).PlayAsync(match.Id, agent, agent, TestContext.Current.CancellationToken);

        var opening = events.Of<PurchasesRevealed>().First().Choices;
        var rollout = new RoundRollout(agent, scorer, TestContent.Resources, rules);
        var value = rollout.Play(origin, Advance.Buy(start, opening, TestContent.Resources), rules.RoundCap, new TestRandom(seed));

        value.ShouldBe(RolloutValue.Of(played.Value, seat, Scored(events, scorer, seat)));
    }

    private static GreedyAgent Greedy(RuleSet rules) => new(TestContent.Resources, rules);

    /// <summary>What the rollout sums, read off the match: each action scored on the board it landed on, the seat's for and the other's against.</summary>
    private static double Scored(Collected events, ActionScorer scorer, PlayerSlot seat) =>
        events.Of<CombatActionResolved>().Sum(resolved =>
        {
            var frame = resolved.Frame.ShouldNotBeNull();
            var actor = resolved.Resolution.Action.Actor;
            var index = frame.Timeline.ToList().FindIndex(slot => slot.Creature == actor);
            var stillToAct = frame.Timeline.Skip(index + 1).Select(slot => slot.Creature).ToHashSet();
            var sign = frame.Before.First(creature => creature.Id == actor).Owner == seat ? 1 : -1;
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

    /// <summary>The agent it wraps, but every tie of its own creatures seated in reverse.</summary>
    private sealed class TiesReversed(IPlayerAgent inner) : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => inner.DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => inner.DecideSpeed(board, creature);

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
            [.. options.Ties.SelectMany(tie => tie.Reverse())];

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => inner.DecideIntent(board, intentOption);

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => inner.DecideTargets(board, options);
    }
}
