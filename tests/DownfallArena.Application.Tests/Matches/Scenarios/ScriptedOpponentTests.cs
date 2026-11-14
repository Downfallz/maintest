using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Scenarios;

/// <summary>
/// The seat a person does not play in a match scenario: the source's recorded answers while they answer what is
/// asked, then a bot for good (docs/tabletop/scenarios-plan.md, stage S2).
/// </summary>
public sealed class ScriptedOpponentTests
{
    private static readonly CreatureId First = CreatureId.From(1);

    [Theory]
    [InlineData(CombatDeclaration.AtActivation)]
    [InlineData(CombatDeclaration.HiddenIntent)]
    public async Task Two_scripted_seats_play_a_scenario_to_its_source_s_end_and_neither_takes_over(CombatDeclaration declaration)
    {
        var (replica, scenario, started, outcome) = await StartedAsync(declaration);
        var first = ScriptedOpponent.Of(scenario, PlayerSlot.Player1, Unasked());
        var second = ScriptedOpponent.Of(scenario, PlayerSlot.Player2, Unasked());

        var step = await replica.Stepper.AdvanceAsync(started.Match.MatchId, new MatchSeats(first, second), HeldAnswers.None, TestContext.Current.CancellationToken);

        step.Value.Outcome.ShouldBe(outcome);
        first.TookOver.ShouldBeFalse();
        second.TookOver.ShouldBeFalse();
    }

    [Theory]
    [InlineData(CombatDeclaration.AtActivation)]
    [InlineData(CombatDeclaration.HiddenIntent)]
    public async Task A_seat_whose_recorded_answers_run_out_hands_over_to_its_bot_once_and_the_match_plays_out(CombatDeclaration declaration)
    {
        var (replica, scenario, started, _) = await StartedAsync(declaration);
        var recorded = scenario.After.OfType<DecisionEntry>().Where(entry => entry.Decision.Slot == PlayerSlot.Player2).Select(entry => entry.Decision.Decision);
        var opponent = new ScriptedOpponent(recorded.Take(2), new RandomAgent(new TestRandom(41)));
        var handovers = 0;
        opponent.HandedOver += (_, _) => handovers++;

        var step = await replica.Stepper.AdvanceAsync(started.Match.MatchId, new MatchSeats(ScriptedOpponent.Of(scenario, PlayerSlot.Player1, new RandomAgent(new TestRandom(97))), opponent), HeldAnswers.None, TestContext.Current.CancellationToken);

        step.Value.Outcome.ShouldNotBeNull();
        opponent.TookOver.ShouldBeTrue();
        handovers.ShouldBe(1);
    }

    [Fact]
    public void A_recorded_answer_the_options_do_not_offer_hands_over_for_good()
    {
        var fallback = Substitute.For<IPlayerAgent>();
        fallback.DecideIntent(Arg.Any<PlayerBoardState>(), Arg.Any<IntentOption>()).Returns(TestContent.Guard);
        var opponent = new ScriptedOpponent([PlayerDecision.DeclareIntent(First, TestContent.Slam), PlayerDecision.DeclareIntent(First, TestContent.Strike)], fallback);
        var offered = new IntentOption(First, [TestContent.Strike, TestContent.Guard]);

        var answers = new[] { opponent.DecideIntent(null!, offered), opponent.DecideIntent(null!, offered) };

        answers.ShouldBe([TestContent.Guard, TestContent.Guard]);
        opponent.TookOver.ShouldBeTrue();
    }

    [Fact]
    public void A_recorded_answer_about_another_creature_is_not_the_answer_to_this_question()
    {
        var fallback = Substitute.For<IPlayerAgent>();
        fallback.DecideSpeed(Arg.Any<PlayerBoardState>(), Arg.Any<CreatureId>()).Returns(Domain.Matches.Rounds.Speed.Standard);
        var opponent = new ScriptedOpponent([PlayerDecision.ChooseSpeed(CreatureId.From(2), Domain.Matches.Rounds.Speed.Quick)], fallback);

        var speed = opponent.DecideSpeed(null!, First);

        speed.ShouldBe(Domain.Matches.Rounds.Speed.Standard);
        opponent.TookOver.ShouldBeTrue();
    }

    /// <summary>A fallback no test expects to be asked: it answers what nobody plays.</summary>
    private static IPlayerAgent Unasked() => Substitute.For<IPlayerAgent>();

    /// <summary>A recorded match of two random agents, cut at its middle round and started under an id of its own.</summary>
    private static async Task<(Replica Replica, ScenarioCut Scenario, ScenarioStart Started, MatchOutcome? Outcome)> StartedAsync(CombatDeclaration declaration)
    {
        var replica = new Replica(new JournalStore());
        var rules = MatchStore.TwoOnTwo().WithCombatDeclaration(declaration);
        var source = new JournaledMatch("run-1", MatchId.New(), rules, 2, MatchStore.Roster(rules));
        var played = await replica.Journaled.AdvanceAsync(source, new MatchSeats(new RandomAgent(new TestRandom(3)), new RandomAgent(new TestRandom(4))), TestContext.Current.CancellationToken);
        IReadOnlyList<IJournalEntry> entries = [.. played.Value.Step.Decided.Select(decision => new DecisionEntry(decision))];
        var rounds = new ScenarioRounds(replica.Replay, replica.Stepper, replica.Store.Repository, []);
        var reading = await rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken);
        var cut = await new ScenarioCutter(rounds, replica.Replay, replica.Stepper, replica.Store.Repository, [])
            .CutAsync(source, entries, reading.Rounds[reading.Rounds.Count / 2].Round, TestContext.Current.CancellationToken);
        var started = await new ScenarioStarter(replica.Replay, replica.Stepper).StartAsync(cut.Value, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);
        return (replica, cut.Value, started.Value, played.Value.Step.Outcome);
    }
}
