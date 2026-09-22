using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Learning.Recording;

public sealed class RecordingAgentTests
{
    private static readonly FeatureSchema Schema = FeatureSchema.Build(TestContent.Resources, MatchStore.TwoOnTwo());
    private static readonly ObservationBuilder Observations = new(Schema);
    private static readonly ActionEncoder Actions = new(Schema);
    private static readonly CandidateTerms Terms = new(TestContent.Resources, MatchStore.TwoOnTwo());

    [Fact]
    public async Task Every_decision_becomes_a_step_whose_action_is_among_the_candidates()
    {
        var store = new MatchStore();
        var match = store.Started(random: new TestRandom(42));
        var steps = new List<StepRecord>();
        var player1 = new CountingAgent(new RandomAgent(new TestRandom(1)));
        var player2 = new CountingAgent(new RandomAgent(new TestRandom(2)));

        var outcome = await Handlers.Driver(store.Workflow).PlayAsync(
            match.Id,
            new RecordingAgent(player1, Observations, Actions, Terms, steps),
            new RecordingAgent(player2, Observations, Actions, Terms, steps),
            TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        steps.Count.ShouldBe(player1.Decisions + player2.Decisions);
        steps.Count(step => step.Slot == PlayerSlot.Player1).ShouldBe(player1.Decisions);
        steps.ShouldAllBe(step => step.MatchId == match.Id);
        steps.ShouldAllBe(step => step.Candidates.Contains(step.Action));
        steps.ShouldAllBe(step => step.Observation.Features.Count == Schema.Length && step.Observation.SchemaId == Schema.Id);
        steps.ShouldAllBe(step => step.CandidateTerms.Count == step.Candidates.Count, "one term vector per candidate (ADR 0051)");
        steps.ShouldAllBe(step => step.CandidateTerms.All(terms => terms.Count == ScoreTerms.Count));
        steps.ShouldAllBe(step => step.Round >= 1 && step.SubPhase != null);
        new[] { ActionKind.Evolve, ActionKind.Speed, ActionKind.Intent, ActionKind.Targets }.ShouldBeSubsetOf(steps.Select(step => step.Kind).Distinct());
    }

    [Fact]
    public void A_pass_is_recorded_as_the_pass_action_among_the_unlocks()
    {
        var inner = Substitute.For<IPlayerAgent>();
        inner.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        var steps = new List<StepRecord>();
        var board = Board();
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.GuardPack])]);

        var decision = new RecordingAgent(inner, Observations, Actions, Terms, steps).DecideEvolution(board, options);

        decision.IsPass.ShouldBeTrue();
        var step = steps.ShouldHaveSingleItem();
        step.Kind.ShouldBe(ActionKind.Pass);
        step.Action.ShouldBe("pass");
        step.Code.ShouldBe(ActionCode.Pass);
        step.Candidates.ShouldBe(["evolve:0:tier:guard:v1", "pass"]);
        step.CandidateTerms.Count.ShouldBe(2);
        step.CandidateTerms[1].ShouldAllBe(term => term == 0f, "a pass reads zero on every term");
        step.Slot.ShouldBe(PlayerSlot.Player1);
        step.Round.ShouldBe(1);
        step.SubPhase.ShouldBe(RoundSubPhase.Evolution);
        step.Observation.ShouldBe(Observations.Build(board));
    }

    [Fact]
    public void Speed_intent_and_target_steps_name_the_acting_slot_and_the_spell()
    {
        var inner = Substitute.For<IPlayerAgent>();
        inner.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(Speed.Quick);
        inner.DecideIntent(Arg.Any<PlayerBoardState>(), Arg.Any<IntentOption>()).Returns(TestContent.Strike);
        inner.DecideTargets(Arg.Any<PlayerBoardState>(), Arg.Any<TargetOptions>()).Returns([CreatureId.From(4)]);
        var steps = new List<StepRecord>();
        var agent = new RecordingAgent(inner, Observations, Actions, Terms, steps);
        var board = Board();

        agent.DecideSpeed(board, CreatureId.From(2)).ShouldBe(Speed.Quick);
        agent.DecideIntent(board, new IntentOption(CreatureId.From(1), [TestContent.Strike])).ShouldBe(TestContent.Strike);
        agent.DecideTargets(board, new TargetOptions(CreatureId.From(1), TestContent.Strike, new LegalTargets(1, 1, [CreatureId.From(3), CreatureId.From(4)])))
            .ShouldBe([CreatureId.From(4)]);

        steps.Select(step => step.Action).ShouldBe(["speed:1:Quick", "intent:0:spell:strike:v1", "targets:0:spell:strike:v1:3"]);
        steps.Select(step => step.Kind).ShouldBe([ActionKind.Speed, ActionKind.Intent, ActionKind.Targets]);
        steps[0].Candidates.ShouldBe(["speed:1:Quick", "speed:1:Standard"]);
        steps[2].Candidates.ShouldBe(["targets:0:spell:strike:v1:2", "targets:0:spell:strike:v1:3"]);
        steps[2].Code.TargetMask.ShouldBe(0b1000);
    }

    [Fact]
    public void A_choice_the_options_do_not_offer_is_refused()
    {
        var inner = Substitute.For<IPlayerAgent>();
        inner.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>())
            .Returns(EvolutionDecision.Unlock(new EvolutionChoice(CreatureId.From(1), TestContent.SlamPack)));
        var steps = new List<StepRecord>();
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.GuardPack])]);

        var exception = Should.Throw<InvalidOperationException>(() => new RecordingAgent(inner, Observations, Actions, Terms, steps).DecideEvolution(Board(), options));

        exception.Message.ShouldContain("evolve:0:tier:slam:v1");
        steps.ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        var agent = new RecordingAgent(new RandomAgent(new TestRandom(1)), Observations, Actions, Terms, []);
        var board = Board();

        Should.Throw<ArgumentNullException>(() => agent.DecideEvolution(null!, new EvolutionOptions(1, [])));
        Should.Throw<ArgumentNullException>(() => agent.DecideEvolution(board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideSpeed(null!, CreatureId.From(1)));
        Should.Throw<ArgumentNullException>(() => agent.DecideIntent(null!, new IntentOption(CreatureId.From(1), [])));
        Should.Throw<ArgumentNullException>(() => agent.DecideIntent(board, null!));
        Should.Throw<ArgumentNullException>(() => agent.DecideTargets(null!, new TargetOptions(CreatureId.From(1), TestContent.Strike, new LegalTargets(1, 1, []))));
        Should.Throw<ArgumentNullException>(() => agent.DecideTargets(board, null!));
    }

    /// <summary>
    /// A step says who decided it, and the recorder asks rather than assumes: who is playing a seat can change
    /// between one decision and the next, so the name is not a property of the run.
    /// </summary>
    [Fact]
    public void A_step_names_whoever_decided_it()
    {
        var inner = Substitute.For<IPlayerAgent>();
        inner.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        var steps = new List<StepRecord>();
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.GuardPack])]);
        var recording = new RecordingAgent(inner, Observations, Actions, Terms, steps, _ => new Decider(inner, "human:mk"));

        recording.DecideEvolution(Board(), options);

        steps.ShouldHaveSingleItem().DecidedBy.ShouldBe("human:mk");
    }

    /// <summary>
    /// Nothing named the decider, so the step claims nothing. Null has to survive to the file: a reader that
    /// saw a bot's name here would fold the run into a training set, and one that saw nothing knows not to.
    /// </summary>
    [Fact]
    public void A_step_nobody_named_a_decider_for_claims_none()
    {
        var inner = Substitute.For<IPlayerAgent>();
        inner.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        var steps = new List<StepRecord>();
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.GuardPack])]);

        new RecordingAgent(inner, Observations, Actions, Terms, steps).DecideEvolution(Board(), options);

        steps.ShouldHaveSingleItem().DecidedBy.ShouldBeNull();
    }

    /// <summary>
    /// The agent named is the agent asked, so a step cannot carry one player's name over another's decision.
    /// </summary>
    /// <remarks>
    /// Naming a decider and then asking the wrapped agent would be two readings of a seat, and a seat changes
    /// hands while a match runs: the pilot swaps one while the driver is deciding, and a swap landing between
    /// the two reads would be invisible in the record. Here the wrapped agent and the named one answer
    /// differently, so a recorder that asked the wrapped one would write a step the named one did not make.
    /// </remarks>
    [Fact]
    public void A_step_is_answered_by_the_agent_it_names()
    {
        var wrapped = Substitute.For<IPlayerAgent>();
        wrapped.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>())
            .Returns(EvolutionDecision.Unlock(new EvolutionChoice(CreatureId.From(1), TestContent.GuardPack)));
        var named = Substitute.For<IPlayerAgent>();
        named.DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>()).Returns(EvolutionDecision.Pass);
        var steps = new List<StepRecord>();
        var options = new EvolutionOptions(2, [new EvolutionOption(CreatureId.From(1), [TestContent.GuardPack])]);

        var decision = new RecordingAgent(inner: wrapped, Observations, Actions, Terms, steps, _ => new Decider(named, "human:mk"))
            .DecideEvolution(Board(), options);

        decision.IsPass.ShouldBeTrue("the named agent is the one that answered");
        wrapped.DidNotReceive().DecideEvolution(Arg.Any<PlayerBoardState>(), Arg.Any<EvolutionOptions>());
        var step = steps.ShouldHaveSingleItem();
        step.DecidedBy.ShouldBe("human:mk");
        step.Kind.ShouldBe(ActionKind.Pass);
    }

    private static PlayerBoardState Board() => PlayerBoardStateProjection.Build(new MatchStore().Started(), PlayerSlot.Player1);

    private sealed class CountingAgent(IPlayerAgent inner) : IPlayerAgent
    {
        public int Decisions { get; private set; }

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
        {
            Decisions++;
            return inner.DecideEvolution(board, options);
        }

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature)
        {
            Decisions++;
            return inner.DecideSpeed(board, creature);
        }

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
        {
            Decisions++;
            return inner.DecideIntent(board, intentOption);
        }

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
        {
            Decisions++;
            return inner.DecideTargets(board, options);
        }
    }
}
