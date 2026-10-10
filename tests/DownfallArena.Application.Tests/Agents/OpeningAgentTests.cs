using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Agents;

public sealed class OpeningAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly GreedyAgent Greedy = new(TestContent.Resources, Rules);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);

    private static readonly EvolutionOptions BothCanBuyEither =
        new(2, [new EvolutionOption(One, [TestContent.GuardPack, TestContent.JabPack]), new EvolutionOption(Two, [TestContent.GuardPack, TestContent.JabPack])]);

    /// <summary>ADR 0109: round 1 buys the opening's next package, on the creature the inner agent prefers among those that can buy it.</summary>
    [Fact]
    public void Round_one_buys_the_next_package_of_the_opening()
    {
        var agent = new OpeningAgent([TestContent.JabPack, TestContent.JabPack], Greedy);

        var first = agent.DecideEvolution(Board(round: 1), BothCanBuyEither).Choice;

        first.ShouldNotBeNull().Tier.ShouldBe(TestContent.JabPack);
    }

    /// <summary>An opening of two of one package buys it twice: the round's picks so far are taken off the list one for one.</summary>
    [Fact]
    public void An_opening_of_two_of_a_package_buys_it_with_both_picks()
    {
        var agent = new OpeningAgent([TestContent.JabPack, TestContent.JabPack], Greedy);
        var options = new EvolutionOptions(1, [new EvolutionOption(Two, [TestContent.GuardPack, TestContent.JabPack])]);

        var second = agent.DecideEvolution(Board(round: 1) with { EvolutionChoices = [new EvolutionChoice(One, TestContent.JabPack)] }, options).Choice;

        second.ShouldBe(new EvolutionChoice(Two, TestContent.JabPack));
    }

    /// <summary>Once the opening is bought, the rest of round 1 is the inner agent's: an opening of one package leaves the second pick free.</summary>
    [Fact]
    public void A_pick_the_opening_does_not_name_is_the_inner_agents()
    {
        var agent = new OpeningAgent([TestContent.JabPack], Greedy);
        var board = Board(round: 1) with { EvolutionChoices = [new EvolutionChoice(One, TestContent.JabPack)] };
        var options = new EvolutionOptions(1, [new EvolutionOption(Two, [TestContent.GuardPack, TestContent.JabPack])]);

        agent.DecideEvolution(board, options).Choice.ShouldBe(Greedy.DecideEvolution(board, options).Choice);
    }

    /// <summary>Past round 1 the agent is its inner agent, purchases included.</summary>
    [Fact]
    public void After_round_one_the_agent_decides_as_its_inner_agent()
    {
        var agent = new OpeningAgent([TestContent.JabPack, TestContent.JabPack], Greedy);
        var board = Board(round: 3);

        agent.DecideEvolution(board, BothCanBuyEither).Choice.ShouldBe(Greedy.DecideEvolution(board, BothCanBuyEither).Choice);
        agent.DecideSpeed(board, One).ShouldBe(Greedy.DecideSpeed(board, One));
    }

    /// <summary>When no creature can buy what is left of the opening, the inner agent picks from everything on offer.</summary>
    [Fact]
    public void An_opening_no_creature_can_buy_leaves_the_pick_to_the_inner_agent()
    {
        var agent = new OpeningAgent([TestContent.SlamPack], Greedy);
        var board = Board(round: 1);

        agent.DecideEvolution(board, BothCanBuyEither).Choice.ShouldBe(Greedy.DecideEvolution(board, BothCanBuyEither).Choice);
    }

    /// <summary>The spec names packages by name, joined by '+', and Greedy plays them unless a third segment names another agent.</summary>
    [Fact]
    public void An_opening_spec_names_its_packages_and_wraps_greedy_by_default()
    {
        var factory = new AgentFactory(TestContent.Resources, Substitute.For<IScoringWeightsSource>(), Substitute.For<IPolicySource>());

        var agent = factory.Create(AgentSpec.Parse("opening:jab+guard"), Rules, new TestRandom(1)).ShouldBeOfType<OpeningAgent>();

        agent.Opening.ShouldBe([TestContent.JabPack, TestContent.GuardPack]);
        Should.Throw<ArgumentException>(() => factory.Create(AgentSpec.Parse("opening:nowhere"), Rules, new TestRandom(1)));
        Should.Throw<ArgumentException>(() => factory.Create(AgentSpec.Parse("opening:"), Rules, new TestRandom(1)));
    }

    [Fact]
    public void Null_and_empty_arguments_are_rejected()
    {
        Should.Throw<ArgumentException>(() => new OpeningAgent([], Greedy));
        Should.Throw<ArgumentNullException>(() => new OpeningAgent([TestContent.JabPack], null!));
    }

    private static PlayerBoardState Board(int round) =>
        Boards.Board(PlayerSlot.Player1, [Boards.Creature(1, PlayerSlot.Player1), Boards.Creature(2, PlayerSlot.Player1)], [Boards.Creature(3, PlayerSlot.Player2), Boards.Creature(4, PlayerSlot.Player2)]) with { RoundNumber = round };
}
