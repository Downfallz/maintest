using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rounds;

public sealed class RoundTests
{
    private static readonly CreatureId Knight = CreatureId.From(1);
    private static readonly CreatureId Ghoul = CreatureId.From(4);
    private static readonly SpellId Strike = SpellId.Parse("spell:strike:v1");
    private static readonly SpellId Guard = SpellId.Parse("spell:guard:v1");

    private static readonly CombatTimeline Timeline = CombatTimeline.Of(
    [
        new ActivationSlot(PlayerSlot.Player1, Knight, Speed.Quick, Initiative.Of(5)),
        new ActivationSlot(PlayerSlot.Player2, Ghoul, Speed.Standard, Initiative.Of(3)),
    ]);

    [Fact]
    public void The_first_round_is_number_one_and_starts_with_energy_gain()
    {
        var round = Round.First();

        round.Number.ShouldBe(1);
        round.Id.ShouldBe(RoundId.First);
        round.SubPhase.ShouldBe(RoundSubPhase.EnergyGain);
        round.Phase.ShouldBe(RoundPhase.StartOfRound);
        round.IsFinalized.ShouldBeFalse();
        round.Timeline.ShouldBe(CombatTimeline.Empty);
        round.ToString().ShouldBe("Round 1 (StartOfRound/EnergyGain)");
    }

    [Fact]
    public void The_next_round_takes_the_next_number()
    {
        Round.First().Next().Next().Number.ShouldBe(3);
    }

    [Fact]
    public void Advancing_walks_the_flow_in_order_and_stops_at_finalization()
    {
        var round = Round.First();

        foreach (var expected in RoundFlow.Steps.Skip(1))
        {
            round.Advance();
            round.SubPhase.ShouldBe(expected);
            round.Phase.ShouldBe(RoundFlow.PhaseOf(expected));
        }

        round.IsFinalized.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(round.Advance);
    }

    [Fact]
    public void Evolution_choices_are_accepted_once_each_during_evolution_only()
    {
        var round = Round.First();
        var choice = new EvolutionChoice(Knight, Guard);

        round.SubmitEvolutionChoice(PlayerSlot.Player1, choice).Error.ShouldBe(RoundErrors.EvolutionNotOpen);

        AdvanceTo(round, RoundSubPhase.Evolution);

        round.SubmitEvolutionChoice(PlayerSlot.Player1, choice).IsSuccess.ShouldBeTrue();
        round.SubmitEvolutionChoice(PlayerSlot.Player1, choice).Error.ShouldBe(RoundErrors.EvolutionAlreadySubmitted);
        round.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(Knight, Strike)).IsSuccess.ShouldBeTrue();
        round.EvolutionChoicesOf(PlayerSlot.Player1).Count.ShouldBe(2);
        round.EvolutionChoicesOf(PlayerSlot.Player2).ShouldBeEmpty();
    }

    [Fact]
    public void Evolution_passes_are_accepted_once_per_player_during_evolution_only()
    {
        var round = Round.First();

        round.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionNotOpen);

        AdvanceTo(round, RoundSubPhase.Evolution);

        round.HasPassedEvolution(PlayerSlot.Player1).ShouldBeFalse();
        round.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        round.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionAlreadyPassed);
        round.HasPassedEvolution(PlayerSlot.Player1).ShouldBeTrue();
        round.HasPassedEvolution(PlayerSlot.Player2).ShouldBeFalse();
    }

    [Fact]
    public void Speed_choices_are_accepted_once_per_creature_during_speed_only()
    {
        var round = Round.First();
        var choice = new SpeedChoice(Knight, Speed.Quick);

        round.SubmitSpeedChoice(choice).Error.ShouldBe(RoundErrors.SpeedNotOpen);

        AdvanceTo(round, RoundSubPhase.Speed);

        round.SubmitSpeedChoice(choice).IsSuccess.ShouldBeTrue();
        round.SubmitSpeedChoice(new SpeedChoice(Knight, Speed.Standard)).Error.ShouldBe(RoundErrors.SpeedAlreadyChosen);
        round.SpeedChoiceOf(Knight).ShouldBe(choice);
        round.SpeedChoiceOf(Ghoul).ShouldBeNull();
        round.SpeedChoices.ShouldBe([choice]);
    }

    [Fact]
    public void The_timeline_is_installed_during_turn_order_resolution_only()
    {
        var round = Round.First();

        Should.Throw<InvalidOperationException>(() => round.SetTimeline(Timeline));

        AdvanceTo(round, RoundSubPhase.TurnOrderResolution);
        round.SetTimeline(Timeline);

        round.Timeline.ShouldBeSameAs(Timeline);
        round.RevealCursor.ShouldBe(TurnCursor.Start);
        round.ResolveCursor.ShouldBe(TurnCursor.Start);
        round.NextSlotToReveal.ShouldBe(Timeline[0]);
        round.AllActionsBound.ShouldBeFalse();
    }

    [Fact]
    public void Intents_are_accepted_once_per_creature_during_intent_selection_and_kept_per_player()
    {
        var round = Round.First();
        var intent = new CombatIntent(Knight, Strike);

        round.SubmitIntent(PlayerSlot.Player1, intent).Error.ShouldBe(RoundErrors.IntentsNotOpen);

        AdvanceTo(round, RoundSubPhase.IntentSelection);

        round.SubmitIntent(PlayerSlot.Player1, intent).IsSuccess.ShouldBeTrue();
        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Knight, Guard)).Error.ShouldBe(RoundErrors.IntentAlreadySubmitted);
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Ghoul, Strike)).IsSuccess.ShouldBeTrue();

        round.HasIntent(Knight).ShouldBeTrue();
        round.IntentOf(Ghoul).ShouldBe(new CombatIntent(Ghoul, Strike));
        round.IntentsOf(PlayerSlot.Player1).ShouldBe([intent]);
        round.IntentsOf(PlayerSlot.Player2).Count.ShouldBe(1);
    }

    [Fact]
    public void Actions_bind_the_revealed_intents_in_timeline_order()
    {
        var round = PlannedRound();
        var knightAction = CombatAction.Bind(new CombatIntent(Knight, Strike), [Ghoul]);
        var ghoulAction = CombatAction.Bind(new CombatIntent(Ghoul, Guard), [Ghoul]);

        round.SubmitAction(knightAction).Error.ShouldBe(RoundErrors.TargetingNotOpen);

        round.Advance();
        round.SubPhase.ShouldBe(RoundSubPhase.RevealAndTarget);
        round.PeekNextIntent().ShouldBe(new CombatIntent(Knight, Strike));

        round.SubmitAction(ghoulAction).Error.ShouldBe(RoundErrors.NotThisCreaturesTurn);
        round.SubmitAction(CombatAction.Bind(new CombatIntent(Knight, Guard), [Ghoul])).Error.ShouldBe(RoundErrors.ActionDoesNotMatchIntent);
        round.SubmitAction(knightAction).IsSuccess.ShouldBeTrue();

        round.RevealCursor.Index.ShouldBe(1);
        round.PeekNextIntent().ShouldBe(new CombatIntent(Ghoul, Guard));
        round.SubmitAction(ghoulAction).IsSuccess.ShouldBeTrue();

        round.AllActionsBound.ShouldBeTrue();
        round.NextSlotToReveal.ShouldBeNull();
        round.PeekNextIntent().ShouldBeNull();
        round.SubmitAction(ghoulAction).Error.ShouldBe(RoundErrors.NothingLeftToReveal);
        round.ActionOf(Knight).ShouldBe(knightAction);
    }

    [Fact]
    public void A_creature_on_the_timeline_without_an_intent_is_an_invariant_violation()
    {
        var round = Round.First();
        AdvanceTo(round, RoundSubPhase.TurnOrderResolution);
        round.SetTimeline(Timeline);
        AdvanceTo(round, RoundSubPhase.RevealAndTarget);

        Should.Throw<InvalidOperationException>(() => round.SubmitAction(CombatAction.Bind(new CombatIntent(Knight, Strike), [])));
    }

    [Fact]
    public void Actions_resolve_in_timeline_order_until_combat_is_resolved()
    {
        var round = PlannedRound();
        round.Advance();
        var knightAction = CombatAction.Bind(new CombatIntent(Knight, Strike), [Ghoul]);
        var ghoulAction = CombatAction.Bind(new CombatIntent(Ghoul, Guard), [Ghoul]);
        round.SubmitAction(knightAction);
        round.SubmitAction(ghoulAction);

        Should.Throw<InvalidOperationException>(round.NextActionToResolve);
        Should.Throw<InvalidOperationException>(round.MarkActionResolved);

        round.Advance();
        round.SubPhase.ShouldBe(RoundSubPhase.ActionResolution);
        round.NextSlotToResolve.ShouldBe(Timeline[0]);
        round.NextActionToResolve().ShouldBe(knightAction);

        round.MarkActionResolved();
        round.NextActionToResolve().ShouldBe(ghoulAction);
        round.IsCombatResolved.ShouldBeFalse();

        round.MarkActionResolved();
        round.IsCombatResolved.ShouldBeTrue();
        round.NextSlotToResolve.ShouldBeNull();
        Should.Throw<InvalidOperationException>(round.NextActionToResolve);
        Should.Throw<InvalidOperationException>(round.MarkActionResolved);
    }

    [Fact]
    public void A_creature_on_the_timeline_without_a_bound_action_is_an_invariant_violation()
    {
        var round = PlannedRound();
        AdvanceTo(round, RoundSubPhase.ActionResolution);

        Should.Throw<InvalidOperationException>(round.NextActionToResolve);
    }

    private static Round PlannedRound()
    {
        var round = Round.First();
        AdvanceTo(round, RoundSubPhase.TurnOrderResolution);
        round.SetTimeline(Timeline);
        AdvanceTo(round, RoundSubPhase.IntentSelection);
        round.SubmitIntent(PlayerSlot.Player1, new CombatIntent(Knight, Strike));
        round.SubmitIntent(PlayerSlot.Player2, new CombatIntent(Ghoul, Guard));
        return round;
    }

    private static void AdvanceTo(Round round, RoundSubPhase subPhase)
    {
        while (round.SubPhase != subPhase)
        {
            round.Advance();
        }
    }
}
