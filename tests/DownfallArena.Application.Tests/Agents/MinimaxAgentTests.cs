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

public sealed class MinimaxAgentTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly LookaheadAgent Lookahead = new(ScoringWeights.Default, TestContent.Resources, Rules);
    private static readonly LookaheadAgent Minimax = new(ScoringWeights.Default, TestContent.Resources, Rules, adversarial: true);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    /// <summary>
    /// Four can Rend One, which the scorer prices above everything (a bleed of nineteen), or Strike Two, which
    /// kills it and takes away the Rend Two has declared. The guess is what Four's own reading picks, Rend;
    /// the worst reply for One's team is the kill, because it costs the round Two's action on top of Two.
    /// </summary>
    [Fact]
    public void The_enemy_reply_is_the_guess_for_the_lookahead_and_the_worst_for_the_minimax()
    {
        var board = FourCanKillTwoWhoDeclaredRend();

        Lookahead.Replies(board, One, TestContent.Strike)[Four].ShouldBe(TestContent.Rend);
        Minimax.Replies(board, One, TestContent.Strike)[Four].ShouldBe(TestContent.Strike);
    }

    [Fact]
    public void An_allys_reply_is_read_the_same_way_by_both()
    {
        var board = FourCanKillTwoWhoDeclaredRend();

        Lookahead.Replies(board, One, TestContent.Strike)[Two].ShouldBe(TestContent.Rend);
        Minimax.Replies(board, One, TestContent.Strike)[Two].ShouldBe(TestContent.Rend);
    }

    [Fact]
    public void An_enemy_with_one_castable_spell_leaves_the_minimax_deciding_as_the_lookahead_does()
    {
        var board = FourCanKillTwoWhoDeclaredRend() with
        {
            Enemies = [Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(3), IsStunned = true, Conditions = [new ConditionSnapshot(Stun.For(1), 1)] }, Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(3) }],
        };
        var options = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));

        Minimax.Replies(board, One, TestContent.Strike)[Four].ShouldBe(TestContent.Strike);
        Minimax.DecideTargets(board, options).ShouldBe(Lookahead.DecideTargets(board, options));
        Minimax.DecideTargets(board, options).ShouldBe([Four]);
    }

    [Fact]
    public void The_minimax_agent_is_stamped_and_seated_like_the_lookahead()
    {
        var factory = Handlers.Agents();

        factory.Create(AgentSpec.Parse("minimax"), Rules, new TestRandom(1)).ShouldBeOfType<LookaheadAgent>().IsAdversarial.ShouldBeTrue();
        factory.Create(AgentSpec.Parse("lookahead"), Rules, new TestRandom(1)).ShouldBeOfType<LookaheadAgent>().IsAdversarial.ShouldBeFalse();
        factory.Resolve(AgentSpec.Parse("minimax")).ShouldBe(new AgentSpec(AgentKind.Minimax));
        factory.Resolve(AgentSpec.Parse("minimax:w.json")).ToString().ShouldBe($"Minimax:w.json@{ScoringWeights.Default.Fingerprint}");
    }

    [Fact]
    public async Task Minimax_plays_a_match_to_its_end_and_replays_identically()
    {
        var first = await DigestAsync();
        var second = await DigestAsync();

        first.Entries.Count.ShouldBe(4);
        first.DifferencesFrom(second).ShouldBeEmpty();
        first.AgentA.ShouldBe("Minimax");
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => Minimax.Replies(null!, One, TestContent.Strike));
        Should.Throw<ArgumentNullException>(() => Minimax.Replies(FourCanKillTwoWhoDeclaredRend(), One, null!));
    }

    /// <summary>
    /// One (Strike) and Two (three health, Rend declared) for Player1; Four (Rend, Strike) for Player2. The
    /// timeline is One, Four, Two.
    /// </summary>
    private static PlayerBoardState FourCanKillTwoWhoDeclaredRend()
    {
        var one = Boards.Creature(1, PlayerSlot.Player1);
        var two = Boards.Creature(2, PlayerSlot.Player1) with { Health = Health.Of(3), DefinitionId = TestContent.Bleeder, KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { DefinitionId = TestContent.Bleeder, KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        return Boards.Board(PlayerSlot.Player1, [one, two], [four]) with
        {
            RoundNumber = 1,
            Intents = [new CombatIntent(Two, TestContent.Rend)],
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
        var stamp = RunStamp.Create(new EngineVersion("abc123def456", false), TestContent.Resources, rules, FeatureSchema.Build(TestContent.Resources, rules), "Minimax", "Greedy", 1);
        var scenario = new EvaluationScenario { RuleSet = rules, Roster = MatchStore.Roster(rules), AgentA = AgentSpec.Parse("minimax"), AgentB = AgentSpec.Greedy, Seeds = [3, 4] };
        return BenchmarkDigest.Of(await runner.RunAsync(scenario, stamp, TestContext.Current.CancellationToken));
    }
}
