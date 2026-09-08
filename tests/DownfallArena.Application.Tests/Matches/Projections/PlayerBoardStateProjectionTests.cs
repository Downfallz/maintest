using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Projections;

public sealed class PlayerBoardStateProjectionTests
{
    [Fact]
    public void A_match_waiting_for_players_has_no_round_and_no_creatures()
    {
        var match = new MatchStore().Empty();

        var board = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);

        board.MatchId.ShouldBe(match.Id);
        board.State.ShouldBe(MatchState.WaitingForPlayers);
        board.ContentHash.ShouldBe("test-content");
        board.RoundNumber.ShouldBeNull();
        board.SubPhase.ShouldBeNull();
        board.Allies.ShouldBeEmpty();
        board.Enemies.ShouldBeEmpty();
        board.Timeline.ShouldBeEmpty();
        board.Outcome.ShouldBeNull();
    }

    [Fact]
    public void Each_player_sees_their_own_choices_and_intents_and_the_public_timeline()
    {
        var match = new MatchStore().Started();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.Guard));
        MatchStore.PassEvolution(match);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick));
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(2), Speed.Standard));
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(3), Speed.Standard));
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(4), Speed.Standard));
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), TestContent.Strike));

        var player1 = PlayerBoardStateProjection.Build(match, PlayerSlot.Player1);
        var player2 = PlayerBoardStateProjection.Build(match, PlayerSlot.Player2);

        player1.RoundNumber.ShouldBe(1);
        player1.Phase.ShouldBe(RoundPhase.Combat);
        player1.SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        player1.Allies.Select(creature => creature.Id).ShouldBe([CreatureId.From(1), CreatureId.From(2)]);
        player1.Enemies.Select(creature => creature.Id).ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
        player1.EvolutionChoices.ShouldBe([new EvolutionChoice(CreatureId.From(1), TestContent.Guard)]);
        player1.HasPassedEvolution.ShouldBeTrue();
        player1.SpeedChoices.Select(choice => choice.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(2)]);
        player1.Intents.ShouldBe([new CombatIntent(CreatureId.From(1), TestContent.Strike)]);
        player1.Timeline.Select(slot => slot.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(2), CreatureId.From(3), CreatureId.From(4)]);
        player1.RevealedActions.ShouldBeEmpty();
        player1.RevealCursor.ShouldBe(0);

        player2.EvolutionChoices.ShouldBeEmpty();
        player2.SpeedChoices.Select(choice => choice.Creature).ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
        player2.Intents.ShouldBeEmpty();
        player2.Timeline.ShouldBe(player1.Timeline);
    }

    [Fact]
    public void Revealed_actions_and_cursors_are_public()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);
        var action = CombatAction.Bind(new CombatIntent(CreatureId.From(1), TestContent.Strike), [CreatureId.From(3)]);
        match.SubmitAction(PlayerSlot.Player1, action).IsSuccess.ShouldBeTrue();

        var board = PlayerBoardStateProjection.Build(match, PlayerSlot.Player2);

        board.SubPhase.ShouldBe(RoundSubPhase.RevealAndTarget);
        board.RevealedActions.ShouldBe([action]);
        board.RevealCursor.ShouldBe(1);
        board.ResolveCursor.ShouldBe(0);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => PlayerBoardStateProjection.Build(null!, PlayerSlot.Player1));
    }
}
