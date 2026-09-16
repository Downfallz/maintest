using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Agents;

public sealed class LookaheadAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly LookaheadAgent Agent = new(ScoringWeights.Default, TestContent.Resources, Rules);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    /// <summary>
    /// Two enemies at three health, one of them stunned and off the timeline, the other about to strike the
    /// ally Two down. Both kills score the same on the board they are cast on, so the one-step reading takes
    /// the first candidate; the round played out says killing Four is what keeps Two alive, and that Two then
    /// finishes Three for the elimination.
    /// </summary>
    [Fact]
    public void Targets_go_to_the_enemy_whose_death_changes_the_rest_of_the_round()
    {
        var board = FourAboutToKillTwo();
        var options = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));

        Agent.DecideTargets(board, options).ShouldBe([Four]);
        new HeuristicAgent(ScoringWeights.Default, TestContent.Resources, Rules).DecideTargets(board, options).ShouldBe([Three], "the one-step reading cannot tell the two kills apart");
    }

    /// <summary>
    /// The same board, with a stun the actor could cast instead: it kills nobody, so under weights that price a
    /// stun at nothing the one-step reading takes the kill. The round played out still takes the stun, because
    /// the board it leaves has Two alive and Three dead, which is worth more than a kill and a dead ally.
    /// </summary>
    [Fact]
    public void The_intent_is_the_spell_whose_round_ends_best_not_the_one_that_scores_best_on_its_own()
    {
        var weights = ScoringWeights.Default with { Stun = 0 };
        var board = FourAboutToKillTwo() with
        {
            Allies = [FourAboutToKillTwo().Allies[0] with { Energy = Energy.Of(2), KnownSpells = new HashSet<SpellId> { TestContent.Slam, TestContent.Strike } }, FourAboutToKillTwo().Allies[1]],
            Timeline = [Slot(One, PlayerSlot.Player1), Slot(Four, PlayerSlot.Player2), Slot(Two, PlayerSlot.Player1)],
        };
        var option = new IntentOption(One, [TestContent.Slam, TestContent.Strike]);

        new LookaheadAgent(weights, TestContent.Resources, Rules).DecideIntent(board, option).ShouldBe(TestContent.Slam);
        new HeuristicAgent(weights, TestContent.Resources, Rules).DecideIntent(board, option).ShouldBe(TestContent.Strike, "a stun priced at nothing loses to a kill on the spot");
    }

    /// <summary>
    /// Four has revealed Guard on itself before One's slot. The one-step replay cannot carry a condition
    /// forward, so it reads the two enemies as equal and takes the first candidate; the hypothetical board
    /// carries the buff, so the strike goes where it lands whole.
    /// </summary>
    [Fact]
    public void Targets_are_bound_on_the_board_the_revealed_actions_leave()
    {
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Energy = Energy.Of(1), KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Guard } };
        // Four first on the board, so a reading that cannot tell the two apart takes it by order.
        var board = Boards.Board(PlayerSlot.Player1, [Boards.Creature(1, PlayerSlot.Player1)], [four, Boards.Creature(3, PlayerSlot.Player2)]) with
        {
            Timeline = [Slot(Four, PlayerSlot.Player2), Slot(One, PlayerSlot.Player1)],
            RevealedActions = [CombatAction.Bind(new CombatIntent(Four, TestContent.Guard), [Four])],
        };
        var options = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Four, Three]));

        Agent.DecideTargets(board, options).ShouldBe([Three]);
        new HeuristicAgent(ScoringWeights.Default, TestContent.Resources, Rules).DecideTargets(board, options).ShouldBe([Four], "the one-step replay does not carry the buff");
    }

    /// <summary>
    /// Under weights that price a kill at nothing, damage at next to nothing and energy at a thousand, Slam
    /// costs two energy and Strike none. Slam kills both enemies and ends the match; Strike kills one. The
    /// match won outranks the score whatever the weights, so the two thousand points of energy Strike keeps
    /// do not buy off the win. (Damage is not zero because a stun on a target the same cast kills scores
    /// nothing, and with nothing to tell them apart the scorer would aim Slam at one enemy only.)
    /// </summary>
    [Fact]
    public void A_round_that_wins_the_match_outranks_any_score_whatever_the_weights()
    {
        var weights = ScoringWeights.Default with { Damage = 0.001, Kill = 0, Energy = 1000 };
        var one = Boards.Creature(1, PlayerSlot.Player1) with { Energy = Energy.Of(2), KnownSpells = new HashSet<SpellId> { TestContent.Slam, TestContent.Strike } };
        var board = Boards.Board(PlayerSlot.Player1, [one], [Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(2) }, Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(2) }]) with
        {
            RoundNumber = 1,
            Timeline = [Slot(One, PlayerSlot.Player1)],
        };

        new LookaheadAgent(weights, TestContent.Resources, Rules).DecideIntent(board, new IntentOption(One, [TestContent.Slam, TestContent.Strike])).ShouldBe(TestContent.Slam);
    }

    [Fact]
    public void An_uncastable_spell_binds_no_target()
    {
        Agent.DecideTargets(FourAboutToKillTwo(), new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))).ShouldBeEmpty();
    }

    [Fact]
    public void Evolution_and_speed_are_the_heuristic_agents()
    {
        var heuristic = new HeuristicAgent(ScoringWeights.Default, TestContent.Resources, Rules);
        var board = FourAboutToKillTwo();
        var evolution = new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.Guard]), new EvolutionOption(Two, [TestContent.Guard])]);

        Agent.DecideEvolution(board, evolution).Choice.ShouldBe(heuristic.DecideEvolution(board, evolution).Choice);
        Agent.DecideSpeed(board, One).ShouldBe(heuristic.DecideSpeed(board, One));
        Agent.DecideSpeed(board, One).ShouldBe(Speed.Quick);
        Agent.Weights.ShouldBe(ScoringWeights.Default);
    }

    [Fact]
    public async Task Lookahead_plays_a_match_to_its_end_and_replays_identically()
    {
        var first = await DigestAsync();
        var second = await DigestAsync();

        first.Entries.Count.ShouldBe(4);
        first.DifferencesFrom(second).ShouldBeEmpty();
        first.AgentA.ShouldBe("Lookahead");
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var board = FourAboutToKillTwo();

        Should.Throw<ArgumentNullException>(() => Agent.DecideIntent(null!, new IntentOption(One, [TestContent.Strike])));
        Should.Throw<ArgumentNullException>(() => Agent.DecideIntent(board, null!));
        Should.Throw<ArgumentNullException>(() => Agent.DecideTargets(null!, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))));
        Should.Throw<ArgumentNullException>(() => Agent.DecideTargets(board, null!));
        Should.Throw<InvalidOperationException>(() => Agent.DecideIntent(board, new IntentOption(One, [])));
    }

    /// <summary>
    /// One and Two (three health) for Player1; Three (three health, stunned, no slot this round) and Four
    /// (three health) for Player2. The timeline is One, Four, Two: Four strikes after One and before Two.
    /// </summary>
    private static PlayerBoardState FourAboutToKillTwo()
    {
        var one = Boards.Creature(1, PlayerSlot.Player1);
        var two = Boards.Creature(2, PlayerSlot.Player1) with { Health = Health.Of(3) };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(3), IsStunned = true, Conditions = [new ConditionSnapshot(Stun.For(1), 1)] };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(3) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]) with
        {
            RoundNumber = 1,
            Timeline = [Slot(One, PlayerSlot.Player1), Slot(Four, PlayerSlot.Player2), Slot(Two, PlayerSlot.Player1)],
        };
    }

    private static ActivationSlot Slot(CreatureId creature, PlayerSlot owner) =>
        new(owner, creature, Speed.Standard, Initiative.Of(5));

    private static async Task<BenchmarkDigest> DigestAsync()
    {
        var store = new MatchStore();
        var runner = new EvaluationRunner(Handlers.Runner(store.Workflow, new TestRandomFactory()));
        var rules = MatchStore.TwoOnTwo(roundCap: 8);
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, FeatureSchema.Build(TestContent.Resources, rules), "Lookahead", "Greedy", 1);
        var scenario = new EvaluationScenario { RuleSet = rules, Roster = MatchStore.Roster(rules), AgentA = AgentSpec.Parse("lookahead"), AgentB = AgentSpec.Greedy, Seeds = [3, 4] };
        return BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));
    }
}
