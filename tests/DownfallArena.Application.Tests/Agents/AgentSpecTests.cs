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
    [InlineData("policy:models/value/v1/policy.json", AgentKind.Policy, "models/value/v1/policy.json")]
    [InlineData("explore:0.1", AgentKind.Explore, "0.1")]
    public void A_spec_is_a_kind_and_an_optional_path(string text, AgentKind kind, string? path)
    {
        AgentSpec.Parse(text).ShouldBe(new AgentSpec(kind, path));
    }

    [Fact]
    public void A_resolved_spec_carries_a_version_after_the_path()
    {
        AgentSpec.Parse("heuristic:w.json@abcd1234").ShouldBe(new AgentSpec(AgentKind.Heuristic, "w.json", "abcd1234"));
        new AgentSpec(AgentKind.Heuristic, "w.json", "abcd1234").ToString().ShouldBe("Heuristic:w.json@abcd1234");
        AgentSpec.Parse("heuristic:w.json@").ShouldBe(new AgentSpec(AgentKind.Heuristic, "w.json"));
    }

    [Fact]
    public void Resolving_a_heuristic_spec_fingerprints_the_weights_it_loads()
    {
        var factory = Handlers.Agents();

        factory.Resolve(AgentSpec.Random).ShouldBe(AgentSpec.Random);
        factory.Resolve(AgentSpec.Greedy).ShouldBe(AgentSpec.Greedy);
        var resolved = factory.Resolve(AgentSpec.Parse("heuristic:w.json"));
        resolved.Version.ShouldBe(ScoringWeights.Default.Fingerprint);
        resolved.ToString().ShouldBe($"Heuristic:w.json@{ScoringWeights.Default.Fingerprint}");
        Should.Throw<ArgumentException>(() => factory.Resolve(new AgentSpec(AgentKind.Heuristic)));
        Should.Throw<ArgumentNullException>(() => factory.Resolve(null!));
    }

    [Fact]
    public void The_fingerprint_changes_with_any_weight()
    {
        ScoringWeights.Default.Fingerprint.ShouldMatch("^[0-9a-f]{8}$");
        ScoringWeights.Default.Fingerprint.ShouldBe(ScoringWeights.Default.Fingerprint);
        (ScoringWeights.Default with { Risk = 2.5 }).Fingerprint.ShouldNotBe(ScoringWeights.Default.Fingerprint);
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
