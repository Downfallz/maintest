using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;
using NSubstitute;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A substituted repository backed by a dictionary, plus the workflow and the matches the tests need.
/// </summary>
internal sealed class MatchStore
{
    public static readonly PlayerId Alice = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly PlayerId Bob = PlayerId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private readonly Dictionary<MatchId, Match> _matches = [];

    public MatchStore()
    {
        Repository = Substitute.For<IMatchRepository>();
        Repository.FindAsync(Arg.Any<MatchId>(), Arg.Any<CancellationToken>())
            .Returns(call => _matches.GetValueOrDefault(call.Arg<MatchId>()));
        Repository.SaveAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _matches[call.Arg<Match>().Id] = call.Arg<Match>();
                Calls.Add("save");
                return Task.CompletedTask;
            });
        Dispatcher = Substitute.For<IDomainEventDispatcher>();
        Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Calls.Add("dispatch");
                return Task.CompletedTask;
            });
        Workflow = new MatchWorkflow(Repository, Dispatcher);
    }

    /// <summary>The port calls in the order they happened: "save" and "dispatch".</summary>
    public List<string> Calls { get; } = [];

    public IMatchRepository Repository { get; }

    public IDomainEventDispatcher Dispatcher { get; }

    public MatchWorkflow Workflow { get; }

    /// <summary>A workflow over the same repository whose dispatcher really calls these listeners.</summary>
    public MatchWorkflow WorkflowWith(params IDomainEventListener[] listeners) => new(Repository, new DomainEventDispatcher(listeners));

    public static RuleSet TwoOnTwo(int roundCap = 30) => RuleSet.Create(2, 2, 2, roundCap, 2.0);

    public static List<CreatureDefinitionId> Roster(RuleSet rules) => [.. Enumerable.Repeat(TestContent.Main, rules.TeamSize)];

    /// <summary>A stored match that waits for players.</summary>
    public Match Empty(RuleSet? rules = null, IRandomSource? random = null)
    {
        var match = Match.Create(MatchId.New(), TestContent.Resources, rules ?? TwoOnTwo(), random ?? new TestRandom(7));
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
    }

    public static void DeclareStrikes(Match match)
    {
        foreach (var slot in match.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, TestContent.Strike)).IsSuccess.ShouldBeTrue();
        }
    }
}
