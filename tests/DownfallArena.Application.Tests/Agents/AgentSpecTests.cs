using DownfallArena.Application.Agents;
using DownfallArena.Application.Tests.Support;

namespace DownfallArena.Application.Tests.Agents;

public sealed class AgentSpecTests
{
    [Theory]
    [InlineData("random", AgentKind.Random, null)]
    [InlineData("Random", AgentKind.Random, null)]
    [InlineData("RANDOM:weights.json", AgentKind.Random, "weights.json")]
    [InlineData("random:", AgentKind.Random, null)]
    [InlineData("greedy", AgentKind.Greedy, null)]
    [InlineData("heuristic:learning/weights/greedy.json", AgentKind.Heuristic, "learning/weights/greedy.json")]
    public void A_spec_is_a_kind_and_an_optional_path(string text, AgentKind kind, string? path)
    {
        AgentSpec.Parse(text).ShouldBe(new AgentSpec(kind, path));
    }

    [Fact]
    public void The_text_form_round_trips()
    {
        AgentSpec.Random.ToString().ShouldBe("Random");
        new AgentSpec(AgentKind.Random, "weights.json").ToString().ShouldBe("Random:weights.json");
        AgentSpec.Parse(new AgentSpec(AgentKind.Random, "a/b.json").ToString()).ShouldBe(new AgentSpec(AgentKind.Random, "a/b.json"));
    }

    [Fact]
    public void Unknown_kinds_are_refused_with_the_known_ones()
    {
        Should.Throw<ArgumentException>(() => AgentSpec.Parse("clever")).Message.ShouldContain("Greedy");
        Should.Throw<ArgumentException>(() => AgentSpec.Parse("7"));
        Should.Throw<ArgumentException>(() => AgentSpec.Parse(" "));
    }

    [Fact]
    public void The_factory_seats_the_kind()
    {
        var factory = Handlers.Agents();
        var rules = MatchStore.TwoOnTwo();

        factory.Create(AgentSpec.Random, rules, new TestRandom(1)).ShouldBeOfType<RandomAgent>();
        factory.Create(AgentSpec.Greedy, rules, new TestRandom(1)).ShouldBeOfType<GreedyAgent>();
        factory.Create(AgentSpec.Parse("heuristic:weights.json"), rules, new TestRandom(1)).ShouldBeOfType<HeuristicAgent>().Weights.ShouldBe(ScoringWeights.Default);
        Should.Throw<ArgumentException>(() => factory.Create(new AgentSpec(AgentKind.Heuristic), rules, new TestRandom(1))).Message.ShouldContain("heuristic:<path>");
        Should.Throw<ArgumentNullException>(() => factory.Create(null!, rules, new TestRandom(1)));
        Should.Throw<ArgumentNullException>(() => factory.Create(AgentSpec.Random, null!, new TestRandom(1)));
        Should.Throw<ArgumentNullException>(() => factory.Create(AgentSpec.Random, rules, null!));
    }

    [Fact]
    public void Weights_must_be_finite()
    {
        ScoringWeights.Default.Validated().ShouldBe(ScoringWeights.Default);
        Should.Throw<ArgumentException>(() => (ScoringWeights.Default with { Kill = double.NaN }).Validated()).Message.ShouldContain("kill");
        Should.Throw<ArgumentException>(() => (ScoringWeights.Default with { Risk = double.PositiveInfinity }).Validated()).Message.ShouldContain("risk");
    }
}
