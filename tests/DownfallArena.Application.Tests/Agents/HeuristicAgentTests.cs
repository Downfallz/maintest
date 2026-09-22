using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Agents;

public sealed class HeuristicAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly HeuristicAgent Agent = new(ScoringWeights.Default, TestContent.Resources, Rules);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    [Fact]
    public void The_intent_is_the_spell_with_the_best_expected_outcome()
    {
        var board = Board(enemyHealth: 20);
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);

        Agent.DecideIntent(board, option).ShouldBe(TestContent.Rend, "a bleed of 19 is worth more than 3 damage");
        new HeuristicAgent(ScoringWeights.Default with { Bleed = 0 }, TestContent.Resources, Rules).DecideIntent(board, option).ShouldBe(TestContent.Strike);
        Agent.DecideIntent(Board(enemyHealth: 3), option).ShouldBe(TestContent.Strike, "a kill beats a bleed");
    }

    /// <summary>
    /// ADR 0039. Creature Two acts first on the timeline and has already declared Strike, which kills the one
    /// enemy left. Hitting a corpse scores nothing at all (ADR 0040), so Guard wins and One spends its round
    /// on itself instead. Both readings this needs are public and were already on the board state: the timeline
    /// says who acts first, and the player's own intents say what it has declared.
    /// </summary>
    [Fact]
    public void A_creature_does_not_aim_at_an_enemy_its_own_team_already_kills_first()
    {
        var board = OneEnemyLeft();
        var option = new IntentOption(One, [TestContent.Guard, TestContent.Strike]);

        Agent.DecideIntent(board, option).ShouldBe(TestContent.Strike, "nothing says the enemy is spoken for");
        Agent.DecideIntent(WithAllyStriking(board), option).ShouldBe(TestContent.Guard, "Two kills it first, so Strike is a wasted round");
    }

    /// <summary>
    /// The two tests the reading has to pass to be a reading of the board and not of the clock: an ally that
    /// declared but acts *after* the actor takes nothing away, and neither does one earlier on the timeline
    /// that has not declared yet. Declaration order is not timeline order, so both are real cases.
    /// </summary>
    [Fact]
    public void An_ally_counts_only_when_it_both_acts_first_and_has_already_declared()
    {
        var board = OneEnemyLeft();
        var option = new IntentOption(One, [TestContent.Guard, TestContent.Strike]);
        var declaredButLater = WithAllyStriking(board) with
        {
            Timeline = [Slot(One), Slot(Two)],
        };
        var firstButSilent = WithAllyStriking(board) with { Intents = [] };

        Agent.DecideIntent(declaredButLater, option).ShouldBe(TestContent.Strike, "Two swings after One, so the enemy is still there");
        Agent.DecideIntent(firstButSilent, option).ShouldBe(TestContent.Strike, "Two acts first but has chosen nothing yet");
    }

    /// <summary>One (Strike, Guard, one energy) and Two beside it, against a single enemy Strike kills.</summary>
    private static PlayerBoardState OneEnemyLeft()
    {
        var one = Boards.Creature(1, PlayerSlot.Player1) with
        {
            Energy = Energy.Of(1),
            KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Guard },
        };
        var two = Boards.Creature(2, PlayerSlot.Player1) with { KnownSpells = new HashSet<SpellId> { TestContent.Strike } };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(3) };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(0) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]);
    }

    /// <summary>The same board, with Two ahead of One on the timeline and already committed to Strike.</summary>
    private static PlayerBoardState WithAllyStriking(PlayerBoardState board) => board with
    {
        Timeline = [Slot(Two), Slot(One)],
        Intents = [new CombatIntent(Two, TestContent.Strike)],
    };

    private static ActivationSlot Slot(CreatureId creature) =>
        new(PlayerSlot.Player1, creature, Speed.Standard, Initiative.Of(5));

    [Fact]
    public void Targets_go_to_the_creature_the_spell_can_kill()
    {
        var board = Board(enemyHealth: 20, enemy1Health: 3);

        Agent.DecideTargets(board, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]))).ShouldBe([Three]);
        Agent.DecideTargets(Board(enemyHealth: 20, enemy2Health: 3), new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]))).ShouldBe([Four]);
    }

    [Fact]
    public void A_multi_target_spell_takes_every_target_worth_taking()
    {
        var board = Board(enemyHealth: 20, actorEnergy: 2, actorSpells: [TestContent.Strike, TestContent.Slam]);

        Agent.DecideTargets(board, new TargetOptions(One, TestContent.Slam, new LegalTargets(1, 2, [Three, Four]))).ShouldBe([Three, Four]);
        Agent.DecideIntent(board, new IntentOption(One, [TestContent.Slam, TestContent.Strike])).ShouldBe(TestContent.Slam);
    }

    [Fact]
    public void An_uncastable_spell_binds_no_target()
    {
        Agent.DecideTargets(Board(enemyHealth: 20), new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))).ShouldBeEmpty();
    }

    [Fact]
    public void Speed_is_quick_only_when_a_kill_is_on_the_table()
    {
        Agent.DecideSpeed(Board(enemyHealth: 3), One).ShouldBe(Speed.Quick);
        Agent.DecideSpeed(Board(enemyHealth: 20), One).ShouldBe(Speed.Standard);
    }

    [Fact]
    public void Evolution_unlocks_the_most_valuable_spell_and_passes_only_when_nothing_is_unlockable()
    {
        var board = Board(enemyHealth: 20);

        Agent.DecideEvolution(board, new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.GuardPack])])).Choice.ShouldBe(new EvolutionChoice(One, TestContent.GuardPack));
        Agent.DecideEvolution(board, new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.GuardPack]), new EvolutionOption(Two, [TestContent.SlamPack])])).Choice
            .ShouldBe(new EvolutionChoice(Two, TestContent.SlamPack), "a two-target stun is worth more than a defense buff");
        Agent.DecideEvolution(board, new EvolutionOptions(2, [])).IsPass.ShouldBeTrue();
    }

    /// <summary>
    /// The reordering the initiative weight exists for (ADR 0018): in this catalogue Guard is worth 1 in
    /// combat against Strike's 3.15, and wins the pick anyway because unlocking it buys 6 initiative. At a
    /// weight of zero the same board picks Strike, so it is the price and not the ordering that decides.
    /// </summary>
    [Fact]
    public void An_evolution_pick_can_be_taken_for_the_initiative_it_buys_rather_than_the_damage()
    {
        var board = Board(enemyHealth: 20, actorSpells: []);
        var options = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.JabPack, TestContent.GuardPack])]);

        new HeuristicAgent(ScoringWeights.Default, TestContent.GuardIsFaster, Rules)
            .DecideEvolution(board, options).Choice.ShouldBe(new EvolutionChoice(One, TestContent.GuardPack));

        new HeuristicAgent(ScoringWeights.Default with { Initiative = 0 }, TestContent.GuardIsFaster, Rules)
            .DecideEvolution(board, options).Choice.ShouldBe(new EvolutionChoice(One, TestContent.JabPack));
    }

    [Fact]
    public void The_greedy_agent_is_the_heuristic_agent_with_the_built_in_weights()
    {
        var greedy = new GreedyAgent(TestContent.Resources, Rules);
        var board = Board(enemyHealth: 20, enemy1Health: 3);
        var option = new IntentOption(Two, [TestContent.Rend, TestContent.Strike]);
        var targets = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));
        var evolution = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.GuardPack])]);

        greedy.DecideIntent(board, option).ShouldBe(Agent.DecideIntent(board, option));
        greedy.DecideTargets(board, targets).ShouldBe(Agent.DecideTargets(board, targets));
        greedy.DecideSpeed(board, One).ShouldBe(Agent.DecideSpeed(board, One));
        greedy.DecideEvolution(board, evolution).Choice.ShouldBe(Agent.DecideEvolution(board, evolution).Choice);
    }

    [Fact]
    public async Task Greedy_plays_a_match_to_its_end_and_replays_identically()
    {
        var first = await DigestAsync();
        var second = await DigestAsync();

        first.Entries.Count.ShouldBe(4);
        first.DifferencesFrom(second).ShouldBeEmpty();
        first.AgentA.ShouldBe("Greedy");
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var board = Board(enemyHealth: 20);

        Should.Throw<ArgumentNullException>(() => Agent.DecideEvolution(null!, new EvolutionOptions(1, [])));
        Should.Throw<ArgumentNullException>(() => Agent.DecideEvolution(board, null!));
        Should.Throw<ArgumentNullException>(() => Agent.DecideSpeed(null!, One));
        Should.Throw<ArgumentNullException>(() => Agent.DecideIntent(board, null!));
        Should.Throw<ArgumentNullException>(() => Agent.DecideTargets(null!, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))));
        Should.Throw<InvalidOperationException>(() => Agent.DecideIntent(board, new IntentOption(One, [])));
        Agent.Weights.ShouldBe(ScoringWeights.Default);
    }

    private static async Task<BenchmarkDigest> DigestAsync()
    {
        var store = new MatchStore();
        var runner = new EvaluationRunner(Handlers.Runner(store.Workflow, new TestRandomFactory()));
        var rules = MatchStore.TwoOnTwo(roundCap: 8);
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, FeatureSchema.Build(TestContent.Resources, rules), "Greedy", "Random", 1);
        var scenario = new EvaluationScenario { RuleSet = rules, Roster = MatchStore.Roster(rules), AgentA = AgentSpec.Greedy, AgentB = AgentSpec.Random, Seeds = [3, 4] };
        return BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));
    }

    /// <summary>Creatures 1 (Strike) and 2 (Strike, Rend) of player 1 facing creatures 3 and 4 of player 2.</summary>
    private static PlayerBoardState Board(int enemyHealth, int? enemy1Health = null, int? enemy2Health = null, int actorEnergy = 0, IReadOnlyList<SpellId>? actorSpells = null)
    {
        var one = Boards.Creature(1, PlayerSlot.Player1) with { Energy = Energy.Of(actorEnergy), KnownSpells = new HashSet<SpellId>(actorSpells ?? [TestContent.Strike]) };
        var two = Boards.Creature(2, PlayerSlot.Player1) with { KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(enemy1Health ?? enemyHealth) };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(enemy2Health ?? enemyHealth) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]);
    }
}
