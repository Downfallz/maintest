using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Projections;

public sealed class PlayerOptionsProjectionTests
{
    private static PlayerOptions Options(Match match, PlayerSlot slot) => PlayerOptionsProjection.Build(match, slot, TestContent.Resources);

    [Fact]
    public void Before_the_start_and_after_the_end_there_is_nothing_to_decide()
    {
        var store = new MatchStore();
        var waiting = store.Empty();
        var ended = store.Started(MatchStore.TwoOnTwo(roundCap: 1));
        MatchStore.PassEvolution(ended);
        MatchStore.ChooseStandard(ended);
        MatchStore.DeclareStrikes(ended);
        foreach (var slot in ended.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            var enemy = slot.Owner == PlayerSlot.Player1 ? CreatureId.From(3) : CreatureId.From(1);
            ended.SubmitAction(slot.Owner, CombatAction.Bind(new CombatIntent(slot.Creature, TestContent.Strike), [enemy])).IsSuccess.ShouldBeTrue();
        }

        var resolution = Options(ended, PlayerSlot.Player1);
        while (ended.State == MatchState.InProgress)
        {
            ended.ResolveNextAction().IsSuccess.ShouldBeTrue();
        }

        Options(waiting, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
        resolution.Kind.ShouldBe(PlayerOptionsKind.Resolution);
        resolution.SubPhase.ShouldBe(RoundSubPhase.ActionResolution);
        Options(ended, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Ended);
    }

    [Fact]
    public void Evolution_lists_the_creatures_that_can_unlock_something_and_the_picks_left()
    {
        var match = new MatchStore().Started();

        var fresh = Options(match, PlayerSlot.Player1);
        fresh.Kind.ShouldBe(PlayerOptionsKind.Evolution);
        fresh.SubPhase.ShouldBe(RoundSubPhase.Evolution);
        var evolution = fresh.Evolution.ShouldNotBeNull();
        evolution.RemainingPicks.ShouldBe(2);
        evolution.Creatures.ShouldBe([new EvolutionOption(CreatureId.From(1), [TestContent.Guard]), new EvolutionOption(CreatureId.From(2), [TestContent.Guard])]);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.Guard)).IsSuccess.ShouldBeTrue();
        var afterOne = Options(match, PlayerSlot.Player1).Evolution.ShouldNotBeNull();
        afterOne.RemainingPicks.ShouldBe(1);
        afterOne.Creatures.ShouldBe([new EvolutionOption(CreatureId.From(1), [TestContent.Slam]), new EvolutionOption(CreatureId.From(2), [TestContent.Guard])]);

        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        Options(match, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
        Options(match, PlayerSlot.Player2).Kind.ShouldBe(PlayerOptionsKind.Evolution);
    }

    [Fact]
    public void Speed_lists_the_creatures_without_a_choice()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick)).IsSuccess.ShouldBeTrue();

        var player1 = Options(match, PlayerSlot.Player1);
        var player2 = Options(match, PlayerSlot.Player2);

        player1.Kind.ShouldBe(PlayerOptionsKind.Speed);
        player1.Speed.ShouldNotBeNull().Missing.ShouldBe([CreatureId.From(2)]);
        player2.Speed.ShouldNotBeNull().Missing.ShouldBe([CreatureId.From(3), CreatureId.From(4)]);

        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(2), Speed.Quick)).IsSuccess.ShouldBeTrue();
        Options(match, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
    }

    [Fact]
    public void Intent_lists_the_timeline_creatures_without_an_intent_and_their_affordable_spells()
    {
        var match = new MatchStore().Started();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.Guard)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.Slam)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        MatchStore.ChooseStandard(match);

        var player1 = Options(match, PlayerSlot.Player1);

        player1.Kind.ShouldBe(PlayerOptionsKind.Intent);
        player1.Intent.ShouldNotBeNull().Creatures.ShouldBe(
        [
            new IntentOption(CreatureId.From(1), [TestContent.Guard, TestContent.Slam, TestContent.Strike]),
            new IntentOption(CreatureId.From(2), [TestContent.Strike]),
        ]);

        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), TestContent.Slam)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(2), TestContent.Strike)).IsSuccess.ShouldBeTrue();
        Options(match, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
        Options(match, PlayerSlot.Player2).Intent.ShouldNotBeNull().Creatures.Select(option => option.Creature).ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
    }

    [Fact]
    public void Target_is_offered_to_the_owner_of_the_next_intent_with_its_legal_targets()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);

        var player1 = Options(match, PlayerSlot.Player1);
        var player2 = Options(match, PlayerSlot.Player2);

        player1.Kind.ShouldBe(PlayerOptionsKind.Target);
        player1.Target.ShouldBe(new TargetOptions(CreatureId.From(1), TestContent.Strike, new LegalTargets(1, 1, [CreatureId.From(3), CreatureId.From(4)])));
        player2.Kind.ShouldBe(PlayerOptionsKind.Waiting);
        player2.SubPhase.ShouldBe(RoundSubPhase.RevealAndTarget);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var match = new MatchStore().Started();

        Should.Throw<ArgumentNullException>(() => PlayerOptionsProjection.Build(null!, PlayerSlot.Player1, TestContent.Resources));
        Should.Throw<ArgumentNullException>(() => PlayerOptionsProjection.Build(match, PlayerSlot.Player1, null!));
    }
}
