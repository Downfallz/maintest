using System.Collections.Concurrent;
using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;
using NSubstitute;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A substituted repository backed by a dictionary, plus the workflow and the matches the tests need.
/// <para>
/// Concurrent, because the repository it stands in for is: `InMemoryMatchRepository` is a
/// `ConcurrentDictionary` and a batch plays its matches at the same time. A double that models a
/// sequential store would fail on the batch runner rather than on anything the test is about.
/// </para>
/// </summary>
internal sealed class MatchStore
{
    public static readonly PlayerId Alice = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly PlayerId Bob = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private readonly ConcurrentDictionary<MatchId, Match> _matches = new();
    private readonly ConcurrentQueue<string> _calls = new();

    public MatchStore()
    {
        Repository = Substitute.For<IMatchRepository>();
        Repository.FindAsync(Arg.Any<MatchId>(), Arg.Any<CancellationToken>())
            .Returns(call => _matches.GetValueOrDefault(call.Arg<MatchId>()));
        Repository.SaveAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _matches[call.Arg<Match>().Id] = call.Arg<Match>();
                _calls.Enqueue("save");
                return Task.CompletedTask;
            });
        Dispatcher = Substitute.For<IDomainEventDispatcher>();
        Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _calls.Enqueue("dispatch");
                return Task.CompletedTask;
            });
        Workflow = new MatchWorkflow(Repository, Dispatcher);
    }

    /// <summary>The port calls in the order they happened: "save" and "dispatch".</summary>
    public IReadOnlyCollection<string> Calls => _calls;

    public IMatchRepository Repository { get; }

    public IDomainEventDispatcher Dispatcher { get; }

    public MatchWorkflow Workflow { get; }

    /// <summary>A workflow over the same repository whose dispatcher really calls these listeners.</summary>
    public MatchWorkflow WorkflowWith(params IDomainEventListener[] listeners) => new(Repository, new DomainEventDispatcher(listeners));

    public static RuleSet TwoOnTwo(int roundCap = 30) => RuleSet.Create(2, 2, 2, roundCap, 2.0);

    public static List<CreatureDefinitionId> Roster(RuleSet rules) => [.. Enumerable.Repeat(TestContent.Main, rules.TeamSize)];

    /// <summary>A stored match that waits for players. Its ties go to Player 1 unless a random source is given.</summary>
    public Match Empty(RuleSet? rules = null, IRandomSource? random = null)
    {
        var match = Match.Create(MatchId.New(), TestContent.Resources, rules ?? TwoOnTwo(), random ?? new FirstToRollWinsTies(new TestRandom(7)));
        _matches[match.Id] = match;
        return match;
    }

    /// <summary>A stored match with Alice as Player1 (creatures 1 and 2) and Bob as Player2 (creatures 3 and 4).</summary>
    public Match Started(RuleSet? rules = null, IRandomSource? random = null)
    {
        var match = Empty(rules, random);
        match.Join(Alice, Roster(match.RuleSet)).IsSuccess.ShouldBeTrue();
        match.Join(Bob, Roster(match.RuleSet)).IsSuccess.ShouldBeTrue();
        match.ClearDomainEvents();
        return match;
    }

    public static void PassEvolution(Match match)
    {
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
    }

    public static void ChooseStandard(Match match)
    {
        foreach (var creature in match.Creatures.Where(creature => creature.IsAlive && !creature.IsStunned))
        {
            match.SubmitSpeedChoice(creature.Owner, new SpeedChoice(creature.Id, Speed.Standard)).IsSuccess.ShouldBeTrue();
        }

        KeepTieOrder(match);
    }

    /// <summary>
    /// Every player who owes a tie order keeps the order the timeline holds (ADR 0063); nothing to do in a
    /// round where no side has a tie of its own.
    /// </summary>
    public static void KeepTieOrder(Match match)
    {
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            var round = match.CurrentRound.ShouldNotBeNull();
            if (round.SubPhase == RoundSubPhase.TieOrder && TieOrderRules.Owes(round.Timeline, slot))
            {
                match.SubmitTieOrder(slot, [.. TieOrderRules.GroupsOf(round.Timeline, slot).SelectMany(group => group)]).IsSuccess.ShouldBeTrue();
            }
        }
    }

    public static void DeclareStrikes(Match match)
    {
        foreach (var slot in match.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, TestContent.Strike)).IsSuccess.ShouldBeTrue();
        }
    }
}
