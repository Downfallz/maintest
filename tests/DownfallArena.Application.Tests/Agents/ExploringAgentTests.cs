using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Agents;

public sealed class ExploringAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly GreedyAgent Greedy = new(TestContent.Resources, Rules);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    /// <summary>A draw of 0.999 is above any rate at most 0.99, so every decision is the greedy one.</summary>
    [Fact]
    public void Below_its_rate_the_agent_decides_exactly_as_greedy_does()
    {
        var agent = Agent(0.5, new ScriptedRandom(999));
        var board = Board(enemyHealth: 20, enemy1Health: 3);
        var intent = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);
        var targets = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));
        var evolution = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.Guard])]);

        agent.DecideIntent(board, intent).ShouldBe(Greedy.DecideIntent(board, intent));
        agent.DecideTargets(board, targets).ShouldBe(Greedy.DecideTargets(board, targets));
        agent.DecideSpeed(board, One).ShouldBe(Greedy.DecideSpeed(board, One));
        agent.DecideEvolution(board, evolution).Choice.ShouldBe(Greedy.DecideEvolution(board, evolution).Choice);
    }

    /// <summary>A draw of 0.0 is below any rate, and the next draw then lands on the first candidate.</summary>
    [Fact]
    public void Above_its_rate_the_agent_decides_at_random_instead()
    {
        var agent = Agent(1.0, new ScriptedRandom(0));
        var board = Board(enemyHealth: 20, enemy1Health: 3);
        var intent = new IntentOption(Two, [TestContent.Strike, TestContent.Rend]);
        var targets = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Four, Three]));

        agent.DecideIntent(board, intent).ShouldBe(TestContent.Strike, "the first castable spell, not the best one");
        Greedy.DecideIntent(board, intent).ShouldBe(TestContent.Rend, "which is what greedy would have played");
        agent.DecideTargets(board, targets).ShouldBe([Four], "the first candidate, not the one it could kill");
        Greedy.DecideTargets(board, targets).ShouldBe([Three]);
    }

    /// <summary>
    /// Passing stays a candidate while an unlock is available, and greedy never takes it there, so exploration
    /// is the only thing that can give that action a sample. The random agent unlocks something every time.
    /// </summary>
    [Fact]
    public void Exploration_can_pass_although_an_unlock_is_available()
    {
        var board = Board(enemyHealth: 20);
        var options = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.Guard])]);

        // 0.0 explores, then 1 of the two candidates (the one unlock, then passing) is the second.
        Agent(1.0, new ScriptedRandom(0, 1)).DecideEvolution(board, options).IsPass.ShouldBeTrue();
        Agent(1.0, new ScriptedRandom(0, 2)).DecideEvolution(board, options).Choice
            .ShouldBe(new EvolutionChoice(One, TestContent.Guard), "an even draw lands on the unlock");
        Greedy.DecideEvolution(board, options).IsPass.ShouldBeFalse("which is why only exploration reaches it");
        new RandomAgent(new ScriptedRandom(0, 1)).DecideEvolution(board, options).IsPass
            .ShouldBeFalse("the random agent unlocks whenever it can, so it cannot stand in for this");
    }

    /// <summary>
    /// The candidates are every (creature, spell) unlock and then passing, each with the same weight; picking a
    /// creature first would favour the one with fewer spells.
    /// </summary>
    [Theory]
    [InlineData(0u, 1, "spell:guard:v1")]
    [InlineData(1u, 2, "spell:slam:v1")]
    [InlineData(2u, 2, "spell:strike:v1")]
    [InlineData(3u, null, null)]
    public void Every_unlock_and_passing_share_the_weight(uint draw, int? creature, string? spell)
    {
        var options = new EvolutionOptions(2, [
            new EvolutionOption(One, [TestContent.Guard]),
            new EvolutionOption(Two, [TestContent.Slam, TestContent.Strike]),
        ]);

        var decision = Agent(1.0, new ScriptedRandom(0, draw)).DecideEvolution(Board(enemyHealth: 20), options);

        decision.Choice.ShouldBe(creature is null ? (EvolutionChoice?)null : new EvolutionChoice(CreatureId.From(creature.Value), SpellId.Parse(spell!)));
    }

    /// <summary>
    /// Every legal target set of an allowed size is one candidate; drawing a size first would weigh the sets of
    /// one size against those of another, which is what the random agent does.
    /// </summary>
    [Theory]
    [InlineData(0u, new[] { 3 })]
    [InlineData(1u, new[] { 4 })]
    [InlineData(2u, new[] { 3, 4 })]
    public void Every_target_set_shares_the_weight(uint draw, int[] expected)
    {
        var options = new TargetOptions(One, TestContent.Slam, new LegalTargets(1, 2, [Three, Four]));

        var targets = Agent(1.0, new ScriptedRandom(0, draw)).DecideTargets(Board(enemyHealth: 20), options);

        CreatureId[] wanted = [.. expected.Select(CreatureId.From)];
        targets.ShouldBe(wanted);
    }

    [Fact]
    public void An_uncastable_spell_binds_no_target_and_no_castable_spell_is_a_bug()
    {
        var agent = Agent(1.0, new ScriptedRandom(0));
        var board = Board(enemyHealth: 20);

        agent.DecideTargets(board, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))).ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => agent.DecideIntent(board, new IntentOption(One, [])));
    }

    [Fact]
    public void A_rate_outside_its_range_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Agent(0.0, new ScriptedRandom(0)));
        Should.Throw<ArgumentOutOfRangeException>(() => Agent(-0.1, new ScriptedRandom(0)));
        Should.Throw<ArgumentOutOfRangeException>(() => Agent(1.5, new ScriptedRandom(0)));
        Should.Throw<ArgumentOutOfRangeException>(() => Agent(double.NaN, new ScriptedRandom(0)));
        Agent(1.0, new ScriptedRandom(0)).Rate.ShouldBe(1.0, "exploring every decision is allowed");
    }

    [Fact]
    public void Its_collaborators_are_required()
    {
        var random = new ScriptedRandom(0);

        Should.Throw<ArgumentNullException>(() => new ExploringAgent(0.5, null!, random));
        Should.Throw<ArgumentNullException>(() => new ExploringAgent(0.5, Greedy, null!));
        Should.Throw<ArgumentNullException>(() => Agent(0.5, new ScriptedRandom(0)).DecideEvolution(Board(enemyHealth: 20), null!));
        Should.Throw<ArgumentNullException>(() => Agent(0.5, new ScriptedRandom(0)).DecideIntent(Board(enemyHealth: 20), null!));
        Should.Throw<ArgumentNullException>(() => Agent(0.5, new ScriptedRandom(0)).DecideTargets(Board(enemyHealth: 20), null!));
    }

    [Theory]
    [InlineData("explore:0.1", 0.1)]
    [InlineData("explore:1", 1.0)]
    [InlineData("EXPLORE:0.25", 0.25)]
    public void The_factory_reads_the_rate_from_the_spec(string text, double rate)
    {
        var spec = AgentSpec.Parse(text);

        Handlers.Agents().Create(spec, Rules, new TestRandom(1)).ShouldBeOfType<ExploringAgent>().Rate.ShouldBe(rate);
        Handlers.Agents().Resolve(spec).ShouldBe(spec, "the rate is the whole identity, so there is no version to add");
    }

    [Theory]
    [InlineData("explore")]
    [InlineData("explore:0")]
    [InlineData("explore:2")]
    [InlineData("explore:soon")]
    public void The_factory_refuses_a_spec_without_a_usable_rate(string text)
    {
        var spec = AgentSpec.Parse(text);

        Should.Throw<ArgumentException>(() => Handlers.Agents().Create(spec, Rules, new TestRandom(1)))
            .Message.ShouldContain("explore:<rate>");
    }

    [Fact]
    public async Task An_exploring_agent_replays_identically_and_does_not_play_like_greedy()
    {
        var explored = await DigestAsync(AgentSpec.Parse("explore:0.5"));

        explored.DifferencesFrom(await DigestAsync(AgentSpec.Parse("explore:0.5"))).ShouldBeEmpty();
        explored.DifferencesFrom(await DigestAsync(AgentSpec.Greedy)).ShouldNotBeEmpty();
    }

    private static ExploringAgent Agent(double rate, IRandomSource source) => new(rate, Greedy, source);

    private static async Task<BenchmarkDigest> DigestAsync(AgentSpec agentA)
    {
        var store = new MatchStore();
        var runner = new EvaluationRunner(Handlers.Runner(store.Workflow, new TestRandomFactory()));
        var rules = MatchStore.TwoOnTwo(roundCap: 8);
        var schema = FeatureSchema.Build(TestContent.Resources, rules);
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, schema, agentA.ToString(), "Greedy", 1);
        var scenario = new EvaluationScenario { RuleSet = rules, Roster = MatchStore.Roster(rules), AgentA = agentA, AgentB = AgentSpec.Greedy, Seeds = [3, 4] };
        return BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));
    }

    /// <summary>Creatures 1 (Strike) and 2 (Strike, Rend) of player 1 facing creatures 3 and 4 of player 2.</summary>
    private static PlayerBoardState Board(int enemyHealth, int? enemy1Health = null)
    {
        var one = Boards.Creature(1, PlayerSlot.Player1);
        var two = Boards.Creature(2, PlayerSlot.Player1) with { KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(enemy1Health ?? enemyHealth) };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]);
    }
}
