using DownfallArena.Application.Matches.Decisions;
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

        Options(waiting, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
        ended.State.ShouldBe(MatchState.Ended);
        Options(ended, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Ended);
    }

    [Fact]
    public void Evolution_lists_the_creatures_that_can_buy_something_and_the_picks_left()
    {
        var match = new MatchStore().Started();

        var fresh = Options(match, PlayerSlot.Player1);
        fresh.Kind.ShouldBe(PlayerOptionsKind.Evolution);
        fresh.SubPhase.ShouldBe(RoundSubPhase.Evolution);
        var evolution = fresh.Evolution.ShouldNotBeNull();
        evolution.RemainingPicks.ShouldBe(2);
        evolution.Creatures.ShouldBe(
        [
            new EvolutionOption(CreatureId.From(1), [TestContent.BothPack, TestContent.GuardPack, TestContent.JabPack]),
            new EvolutionOption(CreatureId.From(2), [TestContent.BothPack, TestContent.GuardPack, TestContent.JabPack]),
        ],
        "every level-1 package is open to every creature: prerequisites are the only gate, so multiclassing is free (ADR 0056)");

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.GuardPack)).IsSuccess.ShouldBeTrue();
        var afterOne = Options(match, PlayerSlot.Player1).Evolution.ShouldNotBeNull();
        afterOne.RemainingPicks.ShouldBe(1);
        afterOne.Creatures.ShouldBe(
        [
            new EvolutionOption(CreatureId.From(2), [TestContent.BothPack, TestContent.GuardPack, TestContent.JabPack]),
        ],
        "the creature that bought Guard buys nothing more this round, Slam included; the other is where it was (ADR 0066)");

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

    /// <summary>On one energy a round, Slam's 2 is out of reach at round 1: offered apart, to plan on (ADR 0107).</summary>
    [Fact]
    public void Intent_lists_the_spells_a_creature_cannot_pay_for_yet_apart_from_the_ones_it_can()
    {
        var match = new MatchStore().Started(RuleSet.Create(2, 1, 2, 30, 2.0));
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.BothPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        MatchStore.ChooseStandard(match);

        var option = Options(match, PlayerSlot.Player1).Intent.ShouldNotBeNull().Creatures.Single(one => one.Creature == CreatureId.From(1));

        option.CastableSpells.ShouldBe([TestContent.Jab, TestContent.Strike]);
        option.UnaffordableSpells.ShouldBe([TestContent.Slam]);
        PlayerDecisionCheck.Validate(Options(match, PlayerSlot.Player1), PlayerDecision.DeclareIntent(CreatureId.From(1), TestContent.Slam)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), TestContent.Slam)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Intent_lists_the_timeline_creatures_without_an_intent_and_their_affordable_spells()
    {
        var match = new MatchStore().Started();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.GuardPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        MatchStore.ChooseStandard(match);

        var player1 = Options(match, PlayerSlot.Player1);

        player1.Kind.ShouldBe(PlayerOptionsKind.Intent);
        player1.Intent.ShouldNotBeNull().Creatures.ShouldBe(
        [
            new IntentOption(CreatureId.From(1), [TestContent.Guard, TestContent.Strike]),
            new IntentOption(CreatureId.From(2), [TestContent.Strike]),
        ]);

        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), TestContent.Guard)).IsSuccess.ShouldBeTrue();
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
        player2.SubPhase.ShouldBe(RoundSubPhase.Activation);
    }

    [Fact]
    public void Target_is_offered_on_the_board_the_earlier_slots_left()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);
        var round = match.CurrentRound.ShouldNotBeNull();
        var first = round.NextSlot.ShouldNotBeNull();
        var victim = CreatureId.From(3);

        match.SubmitAction(first.Owner, CombatAction.Bind(new CombatIntent(first.Creature, TestContent.Strike), [victim])).IsSuccess.ShouldBeTrue();

        var next = round.NextSlot.ShouldNotBeNull();
        Options(match, next.Owner).Kind.ShouldBe(PlayerOptionsKind.Target);
        var board = PlayerBoardStateProjection.Build(match, next.Owner);
        var hit = board.Allies.Concat(board.Enemies).Single(creature => creature.Id == victim);
        hit.Health.Value.ShouldBe(hit.MaxHealth.Value - 3, "the strike before this slot has already resolved");
    }

    /// <summary>
    /// ADR 0110. On one energy a round Slam's 2 is out of reach at round 1, so creature 1, which bought Jab and
    /// Slam and acts first on the initiative they bring, is offered Jab and Strike only, in ordinal id order,
    /// each with the targets it has on the board as it stands.
    /// </summary>
    [Fact]
    public void Under_activation_declaration_the_creature_at_its_slot_is_offered_every_spell_it_can_cast_now_with_its_targets()
    {
        var match = new MatchStore().Started(RuleSet.Create(2, 1, 2, 30, 2.0, combatDeclaration: CombatDeclaration.AtActivation));
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), TestContent.BothPack)).IsSuccess.ShouldBeTrue();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);

        var player1 = Options(match, PlayerSlot.Player1);

        match.CurrentRound.ShouldNotBeNull().NextSlot.ShouldNotBeNull().Creature.ShouldBe(CreatureId.From(1));
        player1.Kind.ShouldBe(PlayerOptionsKind.Action);
        player1.SubPhase.ShouldBe(RoundSubPhase.Activation);
        player1.Target.ShouldBeNull();
        player1.Action.ShouldBe(new ActionOptions(
            CreatureId.From(1),
            [
                new TargetOptions(CreatureId.From(1), TestContent.Jab, new LegalTargets(1, 1, [CreatureId.From(3), CreatureId.From(4)])),
                new TargetOptions(CreatureId.From(1), TestContent.Strike, new LegalTargets(1, 1, [CreatureId.From(3), CreatureId.From(4)])),
            ]));
    }

    [Fact]
    public void Under_activation_declaration_the_seat_whose_creature_is_not_at_the_slot_waits()
    {
        var match = new MatchStore().Started(MatchStore.TwoOnTwo().WithCombatDeclaration(CombatDeclaration.AtActivation));
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        var next = match.CurrentRound.ShouldNotBeNull().NextSlot.ShouldNotBeNull();
        var other = next.Owner == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

        var waiting = Options(match, other);

        waiting.Kind.ShouldBe(PlayerOptionsKind.Waiting);
        waiting.SubPhase.ShouldBe(RoundSubPhase.Activation);
        waiting.Action.ShouldBeNull();
        Options(match, next.Owner).Action.ShouldNotBeNull().Actor.ShouldBe(next.Creature);
    }

    /// <summary>The default rule set declares intents, so its slot asks for targets only, as it always has (ADR 0110).</summary>
    [Fact]
    public void Under_the_hidden_intent_rule_activation_offers_a_target_and_never_an_action()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);
        MatchStore.DeclareStrikes(match);

        var player1 = Options(match, PlayerSlot.Player1);

        match.RuleSet.CombatDeclaration.ShouldBe(CombatDeclaration.HiddenIntent);
        player1.Kind.ShouldBe(PlayerOptionsKind.Target);
        player1.Action.ShouldBeNull();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var match = new MatchStore().Started();

        Should.Throw<ArgumentNullException>(() => PlayerOptionsProjection.Build(null!, PlayerSlot.Player1, TestContent.Resources));
        Should.Throw<ArgumentNullException>(() => PlayerOptionsProjection.Build(match, PlayerSlot.Player1, null!));
    }

    /// <summary>
    /// ADR 0063: every creature ties, the fixture's rolls give Player 1 the first two places, and each seat is
    /// offered its own tie to order; a seat that has given its order waits for the other.
    /// </summary>
    [Fact]
    public void A_tie_order_is_offered_to_each_seat_that_holds_two_places_until_it_gives_one()
    {
        var match = new MatchStore().Started();
        MatchStore.PassEvolution(match);
        foreach (var creature in match.Creatures)
        {
            match.SubmitSpeedChoice(creature.Owner, new SpeedChoice(creature.Id, Speed.Standard)).IsSuccess.ShouldBeTrue();
        }

        var player1 = Options(match, PlayerSlot.Player1);
        player1.Kind.ShouldBe(PlayerOptionsKind.TieOrder);
        player1.TieOrder.ShouldNotBeNull().Ties.ShouldHaveSingleItem().ShouldBe([CreatureId.From(1), CreatureId.From(2)]);

        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(2), CreatureId.From(1)]).IsSuccess.ShouldBeTrue();

        Options(match, PlayerSlot.Player1).Kind.ShouldBe(PlayerOptionsKind.Waiting);
        Options(match, PlayerSlot.Player2).TieOrder.ShouldNotBeNull().AsRolled.ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
    }
}
