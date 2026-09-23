using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class MatchTests
{
    [Fact]
    public void A_match_waits_for_two_players_then_starts_and_runs_the_first_round_to_evolution()
    {
        var match = Table.Empty();
        match.State.ShouldBe(MatchState.WaitingForPlayers);
        match.CurrentRound.ShouldBeNull();

        match.Join(Table.Alice, Table.Roster(match)).Value.ShouldBe(PlayerSlot.Player1);
        match.State.ShouldBe(MatchState.WaitingForPlayers);
        match.SlotOf(Table.Alice).ShouldBe(PlayerSlot.Player1);
        match.SlotOf(Table.Bob).ShouldBeNull();

        match.Join(Table.Bob, Table.Roster(match)).Value.ShouldBe(PlayerSlot.Player2);

        match.State.ShouldBe(MatchState.InProgress);
        match.Players[PlayerSlot.Player2].ShouldBe(Table.Bob);
        Table.TeamOf(match, PlayerSlot.Player1).Creatures.Select(creature => creature.Id).ShouldBe([CreatureId.From(1), CreatureId.From(2)]);
        Table.TeamOf(match, PlayerSlot.Player2).Creatures.Select(creature => creature.Id).ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
        match.ContentHash.ShouldBe(Arena.Resources.Version);
        match.Creatures.Count.ShouldBe(4);
        match.Creatures.ShouldAllBe(creature => creature.Energy == Energy.Of(2));
        var round = match.CurrentRound.ShouldNotBeNull();
        round.Number.ShouldBe(1);
        round.SubPhase.ShouldBe(RoundSubPhase.Evolution);

        match.DomainEvents.Select(domainEvent => domainEvent.GetType()).ShouldBe(
        [
            typeof(PlayerJoined),
            typeof(PlayerJoined),
            typeof(MatchStarted),
            typeof(RoundStarted),
            typeof(SubPhaseEntered),
            typeof(SubPhaseEntered),
            typeof(OngoingEffectsApplied),
            typeof(SubPhaseEntered),
        ]);
        match.DomainEvents.OfType<SubPhaseEntered>().Select(entered => entered.SubPhase)
            .ShouldBe([RoundSubPhase.EnergyGain, RoundSubPhase.OngoingEffects, RoundSubPhase.Evolution]);
        match.DomainEvents.OfType<MatchStarted>().Single().ShouldBe(new MatchStarted(match.Id, Table.Alice, Table.Bob, Arena.Resources.Version));
    }

    [Fact]
    public void Joining_is_refused_with_the_wrong_roster_or_twice_or_after_the_start()
    {
        var match = Table.Empty();

        match.Join(Table.Alice, [Table.Main]).Error.ShouldBe(MatchErrors.WrongTeamSize);
        match.Join(Table.Alice, [Table.Main, CreatureDefinitionId.Parse("creature:ghost:v1")]).Error.ShouldBe(MatchErrors.UnknownCreatureDefinition);
        match.Players.ShouldBeEmpty();
        match.DomainEvents.ShouldBeEmpty();

        match.Join(Table.Alice, Table.Roster(match)).IsSuccess.ShouldBeTrue();
        match.Join(Table.Alice, Table.Roster(match)).Error.ShouldBe(MatchErrors.PlayerAlreadyJoined);
        match.TeamOf(PlayerSlot.Player1).ShouldNotBeNull().Owner.ShouldBe(PlayerSlot.Player1);
        match.TeamOf(PlayerSlot.Player2).ShouldBeNull();

        match.Join(Table.Bob, Table.Roster(match)).IsSuccess.ShouldBeTrue();
        match.Join(PlayerId.New(), Table.Roster(match)).Error.ShouldBe(MatchErrors.AlreadyStarted);
    }

    [Fact]
    public void Player_actions_are_refused_before_the_match_starts()
    {
        var match = Table.Empty();
        var creature = CreatureId.From(1);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(creature, Arena.GuardPack)).Error.ShouldBe(MatchErrors.NotInProgress);
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(MatchErrors.NotInProgress);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(creature, Speed.Quick)).Error.ShouldBe(MatchErrors.NotInProgress);
        match.SubmitTieOrder(PlayerSlot.Player1, [creature]).Error.ShouldBe(MatchErrors.NotInProgress);
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(creature, Arena.Strike)).Error.ShouldBe(MatchErrors.NotInProgress);
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(creature, Arena.Strike), [])).Error.ShouldBe(MatchErrors.NotInProgress);
        match.ResolveNextAction().Error.ShouldBe(MatchErrors.NotInProgress);
    }

    [Fact]
    public void Player_actions_are_refused_outside_their_sub_phase()
    {
        var match = Table.Started();
        var creature = CreatureId.From(1);

        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(creature, Speed.Quick)).Error.ShouldBe(RoundErrors.SpeedNotOpen);
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(creature, Arena.Strike)).Error.ShouldBe(RoundErrors.IntentsNotOpen);
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(creature, Arena.Strike), [])).Error.ShouldBe(RoundErrors.TargetingNotOpen);
        match.ResolveNextAction().Error.ShouldBe(RoundErrors.ResolutionNotOpen);

        Table.PassEvolution(match);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(creature, Arena.GuardPack)).Error.ShouldBe(RoundErrors.EvolutionNotOpen);
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionNotOpen);
    }

    /// <summary>
    /// The Guard package is worth one point of initiative, so one purchase buys one point. Creature 3 belongs
    /// to Player2, whose creatures this fixture's rolls put after Player1's in a tie, which is what makes the
    /// move up the timeline visible.
    /// </summary>
    [Fact]
    public void A_bought_package_raises_the_creature_initiative_and_moves_it_up_the_timeline()
    {
        var match = Table.Started();
        var ghoul = CreatureId.From(3);

        Table.CreatureNumber(match, 3).BaseInitiative.ShouldBe(Initiative.Of(5));

        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(ghoul, Arena.GuardPack)).IsSuccess.ShouldBeTrue();

        Table.CreatureNumber(match, 3).BaseInitiative.ShouldBe(Initiative.Of(6));
        Table.CreatureNumber(match, 3).CurrentInitiative.ShouldBe(Initiative.Of(6));

        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.ChooseStandard(match);

        var timeline = match.CurrentRound.ShouldNotBeNull().Timeline;
        timeline.Slots.Select(slot => slot.Creature).ShouldBe(
            [ghoul, CreatureId.From(1), CreatureId.From(2), CreatureId.From(4)]);
        timeline.Slots[0].Initiative.ShouldBe(Initiative.Of(6));
    }

    [Fact]
    public void An_evolution_choice_buys_the_package_and_the_sub_phase_ends_when_both_players_are_done()
    {
        var match = Table.Started();
        var knight = CreatureId.From(1);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.SlamPack)).Error.ShouldBe(PlanningErrors.TierNotAvailable);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.GuardPack)).Error.ShouldBe(PlanningErrors.CreatureAlreadyEvolved);

        Table.CreatureNumber(match, 1).KnowsSpell(Arena.Guard).ShouldBeTrue();
        Table.CreatureNumber(match, 1).OwnsTier(Arena.GuardPack).ShouldBeTrue();
        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.Evolution);

        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionAlreadyPassed);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(2), Arena.GuardPack)).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.Evolution);

        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();

        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.Speed);
        match.DomainEvents.OfType<EvolutionChoiceSubmitted>().Single().Choice.ShouldBe(new EvolutionChoice(knight, Arena.GuardPack));
        match.DomainEvents.OfType<EvolutionPassed>().Select(passed => passed.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
    }

    /// <summary>
    /// A creature buys at most one package an opportunity, so the two picks go to two creatures (ADR 0066): the
    /// second pick cannot climb the level the first one opened, and the refusal changes nothing, while the
    /// same pick on another creature goes through.
    /// </summary>
    [Fact]
    public void A_creature_buys_at_most_one_package_a_round()
    {
        var match = Table.Started();
        var knight = CreatureId.From(1);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        var initiative = Table.CreatureNumber(match, 1).BaseInitiative;
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.SlamPack)).Error.ShouldBe(PlanningErrors.CreatureAlreadyEvolved);

        Table.CreatureNumber(match, 1).OwnsTier(Arena.SlamPack).ShouldBeFalse();
        Table.CreatureNumber(match, 1).KnowsSpell(Arena.Slam).ShouldBeFalse();
        Table.CreatureNumber(match, 1).BaseInitiative.ShouldBe(initiative, "a refused pick pays no bonus");
        match.DomainEvents.OfType<EvolutionChoiceSubmitted>().Count().ShouldBe(1);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(2), Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 2).OwnsTier(Arena.GuardPack).ShouldBeTrue();
    }

    /// <summary>
    /// The limit is an opportunity's, not the match's: the creature that bought at round 1 buys again at
    /// round 3, the next opportunity, and climbs the level the first purchase opened.
    /// </summary>
    [Fact]
    public void A_creature_that_bought_at_one_opportunity_buys_again_at_the_next()
    {
        var match = Table.Started();
        var knight = CreatureId.From(1);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        Table.PlayRound(match);
        Table.PlayRound(match);

        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(3);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.SlamPack)).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 1).OwnsTier(Arena.SlamPack).ShouldBeTrue();
    }

    [Fact]
    public void Speed_choices_build_the_timeline_and_open_the_intents()
    {
        var match = Table.Started();
        Table.PassEvolution(match);

        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(1), Speed.Quick)).Error.ShouldBe(PlanningErrors.NotYourCreature);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Standard)).Error.ShouldBe(RoundErrors.SpeedAlreadyChosen);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(2), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(3), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.Speed);

        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(4), Speed.Quick)).IsSuccess.ShouldBeTrue();

        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        var timeline = match.DomainEvents.OfType<TimelineBuilt>().Single().Timeline;
        timeline.ShouldBeSameAs(match.CurrentRound.Timeline);
        timeline.Slots.Select(slot => slot.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(4), CreatureId.From(2), CreatureId.From(3)]);
    }

    /// <summary>
    /// ADR 0063: the match rolls its ties on its own random source, so a seeded match replays them and the
    /// seat decides nothing. Creatures 1 and 4 tie as Quick, 2 and 3 as Standard, and the rolls favour Player 2
    /// both times.
    /// </summary>
    [Fact]
    public void The_match_rolls_its_initiative_ties_on_its_random_source()
    {
        var match = Table.Started(random: new ScriptedRolls(6, 17, 9, 14));
        Table.PassEvolution(match);

        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(2), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(3), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(4), Speed.Quick)).IsSuccess.ShouldBeTrue();

        match.CurrentRound.ShouldNotBeNull().Timeline.Slots.Select(slot => slot.Creature)
            .ShouldBe([CreatureId.From(4), CreatureId.From(1), CreatureId.From(3), CreatureId.From(2)]);
        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.IntentSelection, "no side holds two places in one tie, so nobody has an order to give");
        match.DomainEvents.OfType<TiesOrdered>().ShouldBeEmpty("the timeline the rolls built is the one the round plays");
    }

    /// <summary>
    /// ADR 0063: the rolls give Player 1 the first and third places and Player 2 the second and fourth; each
    /// player then puts their own creatures in those places in the order they want.
    /// </summary>
    [Fact]
    public void A_player_orders_their_own_tied_creatures_among_the_places_their_side_won()
    {
        var match = Table.Started(random: new ScriptedRolls(20, 5, 15, 1));
        Table.PassEvolution(match);
        ChooseAllStandard(match);
        var round = match.CurrentRound.ShouldNotBeNull();
        round.SubPhase.ShouldBe(RoundSubPhase.TieOrder);
        round.Timeline.Slots.Select(slot => slot.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(3), CreatureId.From(2), CreatureId.From(4)]);

        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(2), CreatureId.From(1)]).IsSuccess.ShouldBeTrue();
        round.SubPhase.ShouldBe(RoundSubPhase.TieOrder, "Player 2 has a tie of their own to order");
        match.SubmitTieOrder(PlayerSlot.Player2, [CreatureId.From(3), CreatureId.From(4)]).IsSuccess.ShouldBeTrue();

        round.SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        round.Timeline.Slots.Select(slot => slot.Creature).ShouldBe([CreatureId.From(2), CreatureId.From(3), CreatureId.From(1), CreatureId.From(4)]);
        match.DomainEvents.OfType<TieOrderSubmitted>().Select(submitted => submitted.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
        match.DomainEvents.OfType<TiesOrdered>().Single().Timeline.ShouldBeSameAs(round.Timeline);
    }

    [Fact]
    public void A_tie_order_is_refused_outside_its_sub_phase_twice_or_when_it_does_not_name_the_tie()
    {
        var match = Table.Started(random: new ScriptedRolls(20, 5, 15, 1));
        Table.PassEvolution(match);

        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(1), CreatureId.From(2)]).Error.ShouldBe(RoundErrors.TieOrderNotOpen);

        ChooseAllStandard(match);
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(1)]).Error.ShouldBe(PlanningErrors.TieOrderMismatch);
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(1), CreatureId.From(3)]).Error.ShouldBe(PlanningErrors.TieOrderMismatch);
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(1), CreatureId.From(2), CreatureId.From(2)]).Error.ShouldBe(PlanningErrors.TieOrderMismatch);
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(1), CreatureId.From(2)]).IsSuccess.ShouldBeTrue();
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(2), CreatureId.From(1)]).Error.ShouldBe(RoundErrors.TieOrderAlreadySubmitted);
    }

    [Fact]
    public void A_player_whose_creatures_tie_with_none_of_their_own_has_no_order_to_give()
    {
        var match = Table.Started(random: new ScriptedRolls(1, 20, 15));
        Table.PassEvolution(match);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(2), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(3), Speed.Standard)).IsSuccess.ShouldBeTrue();
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(4), Speed.Standard)).IsSuccess.ShouldBeTrue();

        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.TieOrder);
        match.SubmitTieOrder(PlayerSlot.Player1, [CreatureId.From(2)]).Error.ShouldBe(PlanningErrors.NoTieToOrder);
        match.SubmitTieOrder(PlayerSlot.Player2, [CreatureId.From(4), CreatureId.From(3)]).IsSuccess.ShouldBeTrue();

        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        match.CurrentRound.Timeline.Slots.Select(slot => slot.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(4), CreatureId.From(3), CreatureId.From(2)]);
    }

    private static void ChooseAllStandard(Match match)
    {
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            foreach (var creature in Table.Living(match, slot))
            {
                match.SubmitSpeedChoice(slot, new SpeedChoice(creature.Id, Speed.Standard)).IsSuccess.ShouldBeTrue();
            }
        }
    }

    [Fact]
    public void Intents_then_targets_follow_the_timeline_and_open_the_resolution()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);

        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), Arena.Guard)).Error.ShouldBe(CombatErrors.SpellNotKnown);
        Table.DeclareStrikes(match);
        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.RevealAndTarget);

        var first = new CombatIntent(CreatureId.From(1), Arena.Strike);
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(first, [CreatureId.From(3)])).Error.ShouldBe(CombatErrors.NotYourCreature);
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(first, [CreatureId.From(2)])).Error.ShouldBe(CombatErrors.EnemiesOnly);
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(2), Arena.Strike), [CreatureId.From(3)])).Error.ShouldBe(RoundErrors.NotThisCreaturesTurn);
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(first, [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();

        Table.HitFirstLivingEnemy(match);

        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.ActionResolution);
        match.DomainEvents.OfType<ActionRevealed>().Count().ShouldBe(4);
    }

    [Fact]
    public void Resolving_the_last_action_ends_the_round_and_starts_the_next_one()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.HitFirstLivingEnemy(match);

        var steps = Table.ResolveAll(match);

        steps.Count.ShouldBe(4);
        steps.Take(3).ShouldAllBe(step => !step.RoundCompleted && !step.MatchCompleted && step.RoundId == RoundId.First);
        steps[3].RoundId.ShouldBe(RoundId.First);
        steps[3].RoundCompleted.ShouldBeTrue();
        steps[3].MatchCompleted.ShouldBeFalse();
        steps[3].Resolution.Fizzled.ShouldBeFalse();
        steps[0].Resolution.Outcomes.ShouldBe([new DamageOutcome(CreatureId.From(3), 3, false)]);

        Table.CreatureNumber(match, 3).Health.ShouldBe(Health.Of(14));
        Table.CreatureNumber(match, 1).Health.ShouldBe(Health.Of(14));
        match.Creatures.ShouldAllBe(creature => creature.Energy == Energy.Of(4));
        var round = match.CurrentRound.ShouldNotBeNull();
        round.Number.ShouldBe(2);

        // Round 2 offers no evolution opportunity, so the sub-phase completes as it opens and the round is
        // waiting on speeds instead (ADR 0056).
        round.SubPhase.ShouldBe(RoundSubPhase.Speed);
        match.DomainEvents.OfType<CombatActionResolved>().Count().ShouldBe(4);
        match.DomainEvents.OfType<ConditionsExpired>().Single().Expired.ShouldBeEmpty();
        match.DomainEvents.OfType<RoundEnded>().Single().RoundId.ShouldBe(RoundId.First);
        match.DomainEvents.OfType<RoundStarted>().Select(started => started.RoundId.Number).ShouldBe([1, 2]);
        match.DomainEvents.OfType<MatchEnded>().ShouldBeEmpty();
    }

    /// <summary>
    /// The event and the step are how anything outside the aggregate learns what a spell did, so what they
    /// carry has to be what the board took. Publishing the resolution's own outcomes instead would leave every
    /// other test in this suite green.
    /// </summary>
    [Fact]
    public void Resolving_publishes_what_the_board_took_beside_what_the_action_aimed_for()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.HitFirstLivingEnemy(match);

        // Strike deals three; leave the first target with one health so the hit has more to give than it can.
        var target = Table.CreatureNumber(match, 3);
        while (target.Health.Value > 1)
        {
            target.TakeDamage(1);
        }

        var step = match.ResolveNextAction();

        var applied = step.Value.AppliedOutcomes.OfType<DamageOutcome>().Single(outcome => outcome.Target == target.Id);
        applied.Amount.ShouldBe(1, "it had one health to lose, whatever the spell aimed for");
        step.Value.Resolution.Outcomes.OfType<DamageOutcome>().Single().Amount.ShouldBe(3, "what it aimed for is unchanged");
        match.DomainEvents.OfType<CombatActionResolved>().Last().AppliedOutcomes.ShouldBe(step.Value.AppliedOutcomes);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var match = Table.Started();

        Should.Throw<ArgumentNullException>(() => Match.Create(MatchId.New(), null!, RuleSet.Default, new FixedRandom()));
        Should.Throw<ArgumentNullException>(() => Match.Create(MatchId.New(), Arena.Resources, null!, new FixedRandom()));
        Should.Throw<ArgumentNullException>(() => Match.Create(MatchId.New(), Arena.Resources, RuleSet.Default, null!));
        Should.Throw<ArgumentNullException>(() => match.Join(Table.Alice, null!));
        Should.Throw<ArgumentNullException>(() => match.SubmitEvolutionChoice(PlayerSlot.Player1, null!));
        Should.Throw<ArgumentNullException>(() => match.SubmitSpeedChoice(PlayerSlot.Player1, null!));
        Should.Throw<ArgumentNullException>(() => match.SubmitIntent(PlayerSlot.Player1, null!));
        Should.Throw<ArgumentNullException>(() => match.SubmitAction(PlayerSlot.Player1, null!));
    }
}
