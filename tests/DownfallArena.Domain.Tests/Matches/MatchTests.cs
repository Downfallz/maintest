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

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(creature, Arena.Guard)).Error.ShouldBe(MatchErrors.NotInProgress);
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(MatchErrors.NotInProgress);
        match.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(creature, Speed.Quick)).Error.ShouldBe(MatchErrors.NotInProgress);
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

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(creature, Arena.Guard)).Error.ShouldBe(RoundErrors.EvolutionNotOpen);
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionNotOpen);
    }

    [Fact]
    public void An_evolution_choice_unlocks_the_spell_and_the_sub_phase_ends_when_both_players_are_done()
    {
        var match = Table.Started();
        var knight = CreatureId.From(1);

        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.Slam)).Error.ShouldBe(PlanningErrors.SpellNotUnlockable);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(knight, Arena.Guard)).Error.ShouldBe(PlanningErrors.SpellAlreadyKnown);

        Table.CreatureNumber(match, 1).KnowsSpell(Arena.Guard).ShouldBeTrue();
        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.Evolution);

        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(RoundErrors.EvolutionAlreadyPassed);
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(2), Arena.Guard)).Error.ShouldBe(PlanningErrors.NoPicksLeft);
        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.Evolution);

        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();

        match.CurrentRound.SubPhase.ShouldBe(RoundSubPhase.Speed);
        match.DomainEvents.OfType<EvolutionChoiceSubmitted>().Single().Choice.ShouldBe(new EvolutionChoice(knight, Arena.Guard));
        match.DomainEvents.OfType<EvolutionPassed>().Select(passed => passed.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
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
        round.SubPhase.ShouldBe(RoundSubPhase.Evolution);
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
