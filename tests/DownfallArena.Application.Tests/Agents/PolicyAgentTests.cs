using DownfallArena.Application.Agents;
using DownfallArena.Application.Agents.Ports;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;
using NSubstitute;

namespace DownfallArena.Application.Tests.Agents;

public sealed class PolicyAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, Rules);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);
    private const string RendIntent = "intent:1:spell:rend:v1";
    private const string StrikeIntent = "intent:1:spell:strike:v1";

    [Fact]
    public void The_intent_is_the_castable_spell_whose_key_scores_best()
    {
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);

        Agent(Policy((RendIntent, 2.0), (StrikeIntent, 1.0))).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(TestContent.Rend);
        Agent(Policy((RendIntent, 1.0), (StrikeIntent, 2.0))).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(TestContent.Strike);
    }

    [Fact]
    public void A_feature_moves_the_choice()
    {
        var policy = Policy((RendIntent, 2.0), (StrikeIntent, 1.0)) with { Weights = Rows(Schema.Length, (1, "enemy0_health_fraction", 2.0)) };
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);

        Agent(policy).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(TestContent.Strike, "1 + 2 x 1.0 beats 2");
        Agent(policy).DecideIntent(Board(enemyHealth: 3), option).ShouldBe(TestContent.Rend, "1 + 2 x 0.15 does not");
    }

    [Fact]
    public void An_action_the_policy_never_saw_scores_the_fallback()
    {
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);

        Agent(Policy((RendIntent, -5.0)) with { Fallback = 0.0 }).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(TestContent.Strike);
        Agent(Policy((RendIntent, -5.0)) with { Fallback = -10.0 }).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(TestContent.Rend);
    }

    [Fact]
    public void Ties_go_to_the_first_candidate()
    {
        var agent = Agent(Policy());

        agent.DecideIntent(Board(enemyHealth: 20), new IntentOption(Two, [TestContent.Rend, TestContent.Strike])).ShouldBe(TestContent.Rend);
        agent.DecideIntent(Board(enemyHealth: 20), new IntentOption(Two, [TestContent.Strike, TestContent.Rend])).ShouldBe(TestContent.Strike);
    }

    [Fact]
    public void Targets_are_the_best_scoring_set_and_none_when_the_spell_is_uncastable()
    {
        var agent = Agent(Policy(("targets:0:spell:strike:v1:3", 1.0), ("targets:0:spell:strike:v1:2", 0.0)));
        var board = Board(enemyHealth: 20);

        agent.DecideTargets(board, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]))).ShouldBe([Four]);
        agent.DecideTargets(board, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))).ShouldBeEmpty();
    }

    [Fact]
    public void Speed_and_evolution_follow_their_keys()
    {
        var board = Board(enemyHealth: 20);
        var unlock = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.Guard])]);

        Agent(Policy(("speed:0:Quick", 1.0), ("speed:0:Standard", 0.0))).DecideSpeed(board, One).ShouldBe(Speed.Quick);
        Agent(Policy(("speed:0:Quick", 0.0), ("speed:0:Standard", 1.0))).DecideSpeed(board, One).ShouldBe(Speed.Standard);
        Agent(Policy(("evolve:0:spell:guard:v1", 1.0), ("pass", 0.0))).DecideEvolution(board, unlock).Choice.ShouldBe(new EvolutionChoice(One, TestContent.Guard));
        Agent(Policy(("evolve:0:spell:guard:v1", 0.0), ("pass", 1.0))).DecideEvolution(board, unlock).IsPass.ShouldBeTrue();
        Agent(Policy(("pass", -1.0))).DecideEvolution(board, new EvolutionOptions(2, [])).IsPass.ShouldBeTrue("passing is the only candidate");
    }

    [Fact]
    public void The_factory_seats_a_policy_stamped_with_its_fingerprint_and_refuses_another_schema()
    {
        var source = Substitute.For<IPolicySource>();
        source.Load("good.json").Returns(Policy((RendIntent, 1.0)));
        source.Load("other.json").Returns(Policy((RendIntent, 1.0)) with { SchemaId = "features:v1+000000000000" });
        source.Load("reordered.json").Returns(Policy((RendIntent, 1.0)) with { FeatureNames = [.. Schema.FeatureNames.Reverse()] });
        var factory = Handlers.Agents(source);

        factory.Resolve(AgentSpec.Parse("policy:good.json")).ToString().ShouldBe("Policy:good.json@0123abcd");
        factory.Create(AgentSpec.Parse("policy:good.json"), Rules, new TestRandom(1)).ShouldBeOfType<PolicyAgent>().Policy.Fingerprint.ShouldBe("0123abcd");
        Should.Throw<InvalidDataException>(() => factory.Create(AgentSpec.Parse("policy:other.json"), Rules, new TestRandom(1))).Message.ShouldContain(Schema.Id);
        Should.Throw<InvalidDataException>(() => factory.Create(AgentSpec.Parse("policy:reordered.json"), Rules, new TestRandom(1))).Message.ShouldContain("order");
        Should.Throw<ArgumentException>(() => factory.Create(new AgentSpec(AgentKind.Policy), Rules, new TestRandom(1)));
    }

    [Fact]
    public void A_policy_file_is_validated_against_the_contract()
    {
        var policy = Policy((RendIntent, 1.0));

        Should.Throw<InvalidDataException>(() => (policy with { Kind = "tree" }).Validated()).Message.ShouldContain("clone, value");
        Should.Throw<InvalidDataException>(() => (policy with { SchemaVersion = "features:v9", SchemaId = "features:v9+0123456789ab" }).Validated()).Message.ShouldContain("features:v1");
        Should.Throw<InvalidDataException>(() => (policy with { SchemaId = "features:v2+0123456789ab" }).Validated());
        Should.Throw<InvalidDataException>(() => (policy with { ActionKeys = [RendIntent, RendIntent], Weights = Rows(Schema.Length, 2), Bias = [0.0, 0.0] }).Validated());
        Should.Throw<InvalidDataException>(() => (policy with { Bias = [0.0, 0.0] }).Validated());
        Should.Throw<InvalidDataException>(() => (policy with { Weights = Rows(Schema.Length - 1) }).Validated());
        Should.Throw<InvalidDataException>(() => (policy with { Fallback = double.NaN }).Validated());
        Should.Throw<InvalidDataException>(() => (policy with { Bias = [double.PositiveInfinity] }).Validated());
        policy.Validated().ShouldBeSameAs(policy);
    }

    [Fact]
    public void Scoring_needs_an_observation_of_the_policy_width()
    {
        var policy = Policy((RendIntent, 1.0));

        policy.Score("unknown", [1f, 2f]).ShouldBe(policy.Fallback, "an unknown key never reads the features");
        Should.Throw<ArgumentException>(() => policy.Score(RendIntent, [1f, 2f]));
        Should.Throw<ArgumentNullException>(() => policy.Score(null!, []));
        Should.Throw<ArgumentNullException>(() => policy.Score(RendIntent, null!));
    }

    [Fact]
    public void The_baseline_lifts_every_score_by_the_same_amount_and_leaves_the_choice_alone()
    {
        var policy = Policy((RendIntent, 2.0), (StrikeIntent, 1.0));
        var weights = new double[Schema.Length];
        weights[0] = 4.0;
        var lifted = policy with { Baseline = new PolicyBaseline { Weights = weights, Bias = 0.5 } };
        var features = new float[Schema.Length];
        features[0] = 0.25f;
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);

        lifted.Score(RendIntent, features).ShouldBe(policy.Score(RendIntent, features) + 1.5, 1e-9, "4 x 0.25 + 0.5");
        lifted.Score(StrikeIntent, features).ShouldBe(policy.Score(StrikeIntent, features) + 1.5, 1e-9);
        lifted.Score("never-seen", features).ShouldBe(policy.Fallback + 1.5, 1e-9, "an unseen key rides on the position too");
        Agent(lifted).DecideIntent(Board(enemyHealth: 20), option).ShouldBe(Agent(policy).DecideIntent(Board(enemyHealth: 20), option));
    }

    [Fact]
    public void A_baseline_of_another_width_or_an_infinite_one_is_refused()
    {
        var policy = Policy((RendIntent, 1.0));

        Should.Throw<InvalidDataException>(() => (policy with { Baseline = new PolicyBaseline { Weights = new double[Schema.Length - 1], Bias = 0.0 } }).Validated()).Message.ShouldContain("one weight per feature");
        Should.Throw<InvalidDataException>(() => (policy with { Baseline = new PolicyBaseline { Weights = new double[Schema.Length], Bias = double.NaN } }).Validated()).Message.ShouldContain("baseline");
        (policy with { Baseline = new PolicyBaseline { Weights = new double[Schema.Length], Bias = 0.0 } }).Validated().Baseline.ShouldNotBeNull();
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var agent = Agent(Policy());
        var board = Board(enemyHealth: 20);

        Should.Throw<ArgumentNullException>(() => agent.DecideEvolution(null!, new EvolutionOptions(1, [])));
        Should.Throw<ArgumentNullException>(() => agent.DecideEvolution(board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideSpeed(null!, One));
        Should.Throw<ArgumentNullException>(() => agent.DecideIntent(board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideTargets(null!, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))));
        Should.Throw<ArgumentNullException>(() => agent.DecideTargets(board, null!));
        Should.Throw<InvalidOperationException>(() => agent.DecideIntent(board, new IntentOption(One, [])));
    }

    [Fact]
    public async Task A_policy_plays_a_match_to_its_end_and_replays_identically()
    {
        var rules = MatchStore.TwoOnTwo(roundCap: 8);
        var schema = FeatureSchema.Build(TestContent.Resources, rules);
        var source = Substitute.For<IPolicySource>();
        source.Load(Arg.Any<string>()).Returns(Policy(schema));
        var store = new MatchStore();
        var runner = new EvaluationRunner(Handlers.Runner(store.Workflow, new TestRandomFactory(), source));
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, schema, "Policy:p.json@0123abcd", "Random", 1);
        var scenario = new EvaluationScenario { RuleSet = rules, Roster = MatchStore.Roster(rules), AgentA = AgentSpec.Parse("policy:p.json"), AgentB = AgentSpec.Random, Seeds = [3, 4] };

        var first = BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));
        var second = BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));

        first.Entries.Count.ShouldBe(4);
        first.DifferencesFrom(second).ShouldBeEmpty();
    }

    private static PolicyAgent Agent(PolicyFile policy) =>
        new(policy.Validated(), new ObservationBuilder(Schema, TestContent.Resources), new ActionEncoder(Schema));

    /// <summary>A policy under the test schema: the given keys with their bias, every weight zero, no fallback score.</summary>
    private static PolicyFile Policy(params (string Key, double Bias)[] keys) => Policy(Schema, keys);

    /// <summary>The same under another schema, for a match played under other rules (a round cap changes the id).</summary>
    private static PolicyFile Policy(FeatureSchema schema, params (string Key, double Bias)[] keys) => new()
    {
        Kind = "clone",
        SchemaId = schema.Id,
        SchemaVersion = schema.Version,
        FeatureNames = schema.FeatureNames,
        ActionKeys = [.. keys.Select(key => key.Key)],
        Weights = Rows(schema.Length, keys.Length),
        Bias = [.. keys.Select(key => key.Bias)],
        Fallback = -1e9,
        Fingerprint = "0123abcd",
    };

    private static List<IReadOnlyList<double>> Rows(int width, int count = 1) =>
        [.. Enumerable.Range(0, count).Select(_ => (IReadOnlyList<double>)new double[width])];

    /// <summary>Rows of zeros with one weight set: the row of the second key on the named feature.</summary>
    private static List<IReadOnlyList<double>> Rows(int width, (int Row, string Feature, double Weight) weight)
    {
        var rows = Rows(width, 2);
        var row = new double[width];
        row[Schema.IndexOf(weight.Feature)] = weight.Weight;
        rows[weight.Row] = row;
        return rows;
    }

    /// <summary>Creatures 1 (Strike) and 2 (Strike, Rend) of player 1 facing creatures 3 and 4 of player 2.</summary>
    private static PlayerBoardState Board(int enemyHealth)
    {
        var one = Boards.Creature(1, PlayerSlot.Player1);
        var two = Boards.Creature(2, PlayerSlot.Player1) with { KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]);
    }
}
