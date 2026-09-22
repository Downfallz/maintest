using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Application.Tests.Learning;

/// <summary>
/// ADR 0051: the terms a dataset records per candidate are what the heuristic decides on. So the built-in
/// weights applied to a candidate's terms give the heuristic's own score of it, and the action the heuristic
/// takes scores best among the candidates' terms.
/// </summary>
public sealed class CandidateTermsTests
{
    private static readonly RuleSet Rules = MatchStore.TwoOnTwo();
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, Rules);
    private static readonly CandidateTerms Terms = new(TestContent.Resources, Rules);
    private static readonly ActionScorer Scorer = new(TestContent.Resources, Rules, ScoringWeights.Default);
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    [Fact]
    public void The_terms_are_named_after_the_weights_in_their_order()
    {
        CandidateTerms.Names.ShouldBe(ScoringWeights.Default.Named.Select(weight => weight.Name));
        ScoreTerms.Count.ShouldBe(9);
    }

    /// <summary>
    /// The array a dataset records and the names it is read back under come from two hand-written lists, so
    /// the Python side would train on mislabeled columns if either were reordered: every term applied under a
    /// unit weight of its own name reads back as itself, and only itself.
    /// </summary>
    [Fact]
    public void Every_term_reads_back_under_the_weight_of_its_own_name_and_no_other()
    {
        var ordinal = new ScoreTerms(1, 2, 3, 4, 5, 6, 7, 8, 9);
        ordinal.ToArray().ShouldBe(new ScoringWeights(1, 2, 3, 4, 5, 6, 7, 8, 9).Named.Select(weight => weight.Value));

        for (var index = 0; index < ScoreTerms.Count; index++)
        {
            var unit = new double[ScoreTerms.Count];
            unit[index] = 1.0;
            var term = new ScoreTerms(unit[0], unit[1], unit[2], unit[3], unit[4], unit[5], unit[6], unit[7], unit[8]);
            new ScoringWeights(1, 2, 3, 4, 5, 6, 7, 8, 9).Apply(term).ShouldBe(index + 1, $"the term named {ScoreTerms.Names[index]}");
        }
    }

    /// <summary>
    /// The reading is the built-in weights' whatever agent is recorded: under weights that prize damage and
    /// not the kill, the best target set for Strike is the healthy creature, but the intent's recorded terms
    /// are the kill's, the set Greedy binds. A policy trained on such a teacher reads a fixed target set.
    /// </summary>
    [Fact]
    public void An_intents_terms_are_read_under_the_built_in_weights_whatever_the_teacher_plays()
    {
        var board = Board(enemyHealth: 20, fourthHealth: 3);
        var damageOnly = ScoringWeights.Default with { Kill = 0.0, Pressure = 0.0 };
        var scorer = new ActionScorer(TestContent.Resources, Rules, damageOnly);
        var creatures = Foresight.Creatures(board);
        var actor = creatures.First(creature => creature.Id == One);

        var terms = Terms.Intent(board, new IntentOption(One, [TestContent.Strike]));

        var recorded = damageOnly.Apply(Vector(terms[0]));
        var ownBest = scorer.Best(actor, TestContent.Strike, creatures)!.Value.Score;
        terms[0][Index("kill")].ShouldBe(1f, 1e-5f, "the recorded set is the kill Greedy binds");
        ownBest.ShouldBeGreaterThan(recorded, "under its own weights the teacher binds the healthy creature for more damage");
    }

    [Fact]
    public void An_intents_terms_are_those_of_the_target_set_the_built_in_weights_would_bind()
    {
        var board = Board(enemyHealth: 20);
        var creatures = Foresight.Creatures(board);
        var actor = creatures.First(creature => creature.Id == Two);

        var terms = Terms.Intent(board, new IntentOption(Two, [TestContent.Rend, TestContent.Strike]));

        terms.Count.ShouldBe(2);
        Apply(terms[0]).ShouldBe(Scorer.Best(actor, TestContent.Rend, creatures)!.Value.Score, 1e-5);
        Apply(terms[1]).ShouldBe(Scorer.Best(actor, TestContent.Strike, creatures)!.Value.Score, 1e-5);
    }

    [Fact]
    public void A_target_sets_terms_are_its_own_and_a_kill_reads_on_the_kill_term()
    {
        var board = Board(enemyHealth: 20, fourthHealth: 3);
        var options = new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, [Three, Four]));

        var terms = Terms.Targets(board, options);

        terms.Count.ShouldBe(2);
        // Strike deals 3, a crit 6, at a 5 % crit chance: on twenty health no kill, on three a kill either way.
        terms[0][Index("damage")].ShouldBe((float)((0.95 * 3) + (0.05 * 6)), 1e-5f);
        terms[0][Index("kill")].ShouldBe(0f);
        terms[1][Index("damage")].ShouldBe(3f, 1e-5f);
        terms[1][Index("kill")].ShouldBe(1f, 1e-5f);
        terms[1][Index("pressure")].ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void An_uncastable_spell_a_speed_and_a_pass_read_zero_on_every_term()
    {
        var board = Board(enemyHealth: 20);

        Terms.Targets(board, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))).ShouldHaveSingleItem().ShouldAllBe(term => term == 0f);
        CandidateTerms.Speed().Count.ShouldBe(2);
        CandidateTerms.Speed().ShouldAllBe(vector => vector.All(term => term == 0f));
        Terms.Evolution(board, new EvolutionOptions(2, [])).ShouldHaveSingleItem().ShouldAllBe(term => term == 0f);
    }

    [Fact]
    public void An_unlocks_terms_are_the_heuristics_unlock_value_before_the_weights()
    {
        var board = Board(enemyHealth: 20);
        var creatures = Foresight.Creatures(board);
        var actor = creatures.First(creature => creature.Id == One);

        var terms = Terms.Evolution(board, new EvolutionOptions(2, [new EvolutionOption(One, [TestContent.GuardPack, TestContent.SlamPack])]));

        terms.Count.ShouldBe(3, "two purchases and the pass");
        Apply(terms[0]).ShouldBe(Scorer.PurchaseValue(actor, TestContent.GuardPack, creatures), 1e-5);
        Apply(terms[1]).ShouldBe(Scorer.PurchaseValue(actor, TestContent.SlamPack, creatures), 1e-5);
        terms[2].ShouldAllBe(term => term == 0f);
    }

    /// <summary>
    /// The invariant the whole channel rests on, over a played match: every intent and every target set the
    /// heuristic chose scores best, under its own weights, among the terms recorded beside it. A policy whose
    /// candidate weights are the heuristic's therefore plays the heuristic's combat decisions.
    /// </summary>
    [Fact]
    public async Task The_heuristics_combat_decisions_score_best_among_the_recorded_terms()
    {
        var store = new MatchStore();
        var match = store.Started(random: new TestRandom(7));
        var steps = new List<StepRecord>();
        var observations = new ObservationBuilder(Schema);
        var actions = new ActionEncoder(Schema);
        IPlayerAgent Heuristic() => new HeuristicAgent(ScoringWeights.Default, TestContent.Resources, Rules);

        var outcome = await Handlers.Driver(store.Workflow).PlayAsync(
            match.Id,
            new RecordingAgent(Heuristic(), observations, actions, Terms, steps),
            new RecordingAgent(Heuristic(), observations, actions, Terms, steps),
            TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        var combat = steps.Where(step => step.Kind is ActionKind.Intent or ActionKind.Targets).ToList();
        combat.ShouldNotBeEmpty();
        foreach (var step in combat)
        {
            step.CandidateTerms.Count.ShouldBe(step.Candidates.Count);
            var chosen = Apply(step.CandidateTerms[step.Candidates.ToList().IndexOf(step.Action)]);
            chosen.ShouldBe(step.CandidateTerms.Max(Apply), 1e-5, $"{step.Action} among {string.Join(", ", step.Candidates)}");
        }
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var board = Board(enemyHealth: 20);

        Should.Throw<ArgumentNullException>(() => Terms.Evolution(null!, new EvolutionOptions(1, [])));
        Should.Throw<ArgumentNullException>(() => Terms.Evolution(board, null!));
        Should.Throw<ArgumentNullException>(() => Terms.Intent(null!, new IntentOption(One, [])));
        Should.Throw<ArgumentNullException>(() => Terms.Intent(board, null!));
        Should.Throw<ArgumentNullException>(() => Terms.Targets(null!, new TargetOptions(One, TestContent.Strike, new LegalTargets(1, 1, []))));
        Should.Throw<ArgumentNullException>(() => Terms.Targets(board, null!));
    }

    private static double Apply(IReadOnlyList<float> terms) => ScoringWeights.Default.Apply(Vector(terms));

    private static ScoreTerms Vector(IReadOnlyList<float> terms)
    {
        var values = terms.Select(term => (double)term).ToArray();
        return new ScoreTerms(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
    }

    private static int Index(string name) => CandidateTerms.Names.ToList().IndexOf(name);

    /// <summary>Creatures 1 (Strike) and 2 (Strike, Rend) of player 1 facing creatures 3 and 4 of player 2.</summary>
    private static PlayerBoardState Board(int enemyHealth, int? fourthHealth = null)
    {
        var one = Boards.Creature(1, PlayerSlot.Player1);
        var two = Boards.Creature(2, PlayerSlot.Player1) with { KnownSpells = new HashSet<SpellId> { TestContent.Strike, TestContent.Rend } };
        var three = Boards.Creature(3, PlayerSlot.Player2) with { Health = Health.Of(enemyHealth) };
        var four = Boards.Creature(4, PlayerSlot.Player2) with { Health = Health.Of(fourthHealth ?? enemyHealth) };
        return Boards.Board(PlayerSlot.Player1, [one, two], [three, four]);
    }
}
