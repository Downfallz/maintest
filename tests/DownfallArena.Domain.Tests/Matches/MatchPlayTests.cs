using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches;

/// <summary>
/// Scripted matches played to the end: the roadmap's definition of done for phase 7.
/// </summary>
public sealed class MatchPlayTests
{
    [Fact]
    public void A_scripted_match_plays_to_an_elimination()
    {
        var match = Table.Started();
        var rounds = 0;

        while (match.State == MatchState.InProgress)
        {
            Table.PlayRound(match);
            rounds++;
        }

        // Each side focuses the first living enemy when its slot comes up, so no strike is spent on a creature
        // an earlier slot killed (ADR 0083); Player1 acts first in every slot and wipes Bob's team in round 10.
        rounds.ShouldBe(10);
        match.State.ShouldBe(MatchState.Ended);
        var outcome = match.Outcome.ShouldNotBeNull();
        outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        Table.TeamOf(match, PlayerSlot.Player2).IsDefeated.ShouldBeTrue();
        Table.CreatureNumber(match, 2).Health.ShouldBe(Health.Of(5));
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(10);
        match.DomainEvents.OfType<MatchEnded>().Single().ShouldBe(new MatchEnded(match.Id, RoundId.From(10), outcome));
        match.DomainEvents.OfType<RoundStarted>().Count().ShouldBe(10);
        match.DomainEvents.OfType<RoundEnded>().Count().ShouldBe(10);
        match.DomainEvents.OfType<ConditionsExpired>().Count().ShouldBe(9, "the round of the wipe ends without its cleanup");

        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(MatchErrors.NotInProgress);
        match.Join(PlayerId.New(), Table.Roster(match)).Error.ShouldBe(MatchErrors.AlreadyStarted);
    }

    [Fact]
    public void A_creature_that_died_earlier_in_the_round_is_revealed_and_fizzles()
    {
        var match = Table.Started();
        var steps = new List<CombatActionResolved>();
        for (var round = 0; round < 4; round++)
        {
            steps = Table.PlayRound(match);
        }

        // Round 4: creature 1 kills creature 3, creature 2 aims at creature 4 since 3 is gone, creature 3 is
        // revealed dead and fizzles without Bob being asked, creature 4 kills creature 1.
        steps.Select(step => step.Resolution.FizzleReason).ShouldBe([null, null, CombatErrors.ActorDead, null]);
        steps[1].Resolution.Action.Targets.ShouldBe([CreatureId.From(4)]);
        steps[2].Resolution.Action.Targets.ShouldBeEmpty();
        Table.CreatureNumber(match, 1).IsDead.ShouldBeTrue();
        Table.CreatureNumber(match, 3).IsDead.ShouldBeTrue();
        match.State.ShouldBe(MatchState.InProgress);
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(5);
    }

    [Fact]
    public void A_team_wiped_mid_round_ends_the_match_before_the_remaining_slots()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.CreatureNumber(match, 3).TakeDamage(19);
        Table.CreatureNumber(match, 4).TakeDamage(19);
        var seen = match.DomainEvents.Count;

        Table.HitFirstLivingEnemy(match);

        match.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        match.DomainEvents.Skip(seen).Select(raised => raised.GetType()).ShouldBe(
        [
            typeof(ActionRevealed), typeof(CombatActionResolved),
            typeof(ActionRevealed), typeof(CombatActionResolved),
            typeof(RoundEnded), typeof(MatchEnded),
        ]);
        match.CurrentRound.ShouldNotBeNull().ActivationCursor.Index.ShouldBe(2);
    }

    [Fact]
    public void A_team_wiped_at_upkeep_ends_the_match_before_planning()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.CreatureNumber(match, 3).Apply(Bleed.Of(50, rounds: 2)).ShouldNotBeNull();
        Table.CreatureNumber(match, 4).Apply(Bleed.Of(50, rounds: 2)).ShouldNotBeNull();

        Table.HitFirstLivingEnemy(match);

        match.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        var round = match.CurrentRound.ShouldNotBeNull();
        round.Number.ShouldBe(2);
        round.SubPhase.ShouldBe(RoundSubPhase.OngoingEffects);
        match.DomainEvents.OfType<RoundEnded>().Select(ended => ended.RoundId.Number).ShouldBe([1, 2]);
        match.DomainEvents.OfType<MatchEnded>().Single().RoundId.ShouldBe(RoundId.From(2));
    }

    [Fact]
    public void The_round_cap_ends_the_match_on_total_health()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));

        // Bob's first creature buys the Guard package and guards instead of striking. The package also raises
        // its base initiative to 6 (ADR 0056), so it acts before both of Alice's: it guards, and the two strikes that
        // follow land on 2 defense instead of none. Bob deals less and takes much less, and wins the tiebreak.
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(CreatureId.From(3), Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.ChooseStandard(match);
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(2), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(3), Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(4), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(3), Arena.Guard), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(1), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(2), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        var seen = match.DomainEvents.Count;

        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(4), Arena.Strike), [CreatureId.From(1)])).IsSuccess.ShouldBeTrue();

        match.DomainEvents.Skip(seen).Select(raised => raised.GetType()).ShouldContain(typeof(ConditionsExpired));
        match.State.ShouldBe(MatchState.Ended);
        match.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player2, MatchEndReason.RoundCap));
        Table.TeamOf(match, PlayerSlot.Player1).TotalHealth.ShouldBe(37);
        Table.TeamOf(match, PlayerSlot.Player2).TotalHealth.ShouldBe(38);
        Table.CreatureNumber(match, 3).TotalDefense.ShouldBe(Defense.Of(2));
        match.DomainEvents.OfType<MatchEnded>().Single().RoundId.ShouldBe(RoundId.First);
    }

    [Fact]
    public void Conditions_expire_at_the_cleanup_of_the_following_round()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 2));
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(CreatureId.From(3), Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.ChooseStandard(match);
        foreach (var slot in match.CurrentRound.ShouldNotBeNull().Timeline.Slots)
        {
            var spell = slot.Creature == CreatureId.From(3) ? Arena.Guard : Arena.Strike;
            match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, spell)).IsSuccess.ShouldBeTrue();
        }

        // Creature 3 unlocked Guard, so its base initiative is 6 and it reveals before Alice's two.
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(3), Arena.Guard), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(1), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(2), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(4), Arena.Strike), [CreatureId.From(1)])).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 3).TotalDefense.ShouldBe(Defense.Of(2));

        Table.PlayRound(match);

        match.State.ShouldBe(MatchState.Ended);
        Table.CreatureNumber(match, 3).TotalDefense.ShouldBe(Defense.Of(0));
        var expired = match.DomainEvents.OfType<ConditionsExpired>().Select(cleanup => cleanup.Expired).ToList();
        expired.Count.ShouldBe(2);
        expired[0].ShouldBeEmpty();
        expired[1].Keys.ShouldBe([CreatureId.From(3)]);
        // The condition names the cast it came from, so what it does at upkeep has an owner (ADR 0027).
        expired[1][CreatureId.From(3)].ShouldBe(
        [
            new ConditionSnapshot(
                DefenseBuff.Of(2, Duration.OfRounds(1)),
                0,
                new ConditionSource(CreatureId.From(3), Arena.Guard)),
        ]);
    }

    [Fact]
    public void Stunned_creatures_skip_the_next_round()
    {
        var match = Table.Started();
        // The script needs Slam in round 1, and a creature buys one package a round (ADR 0066): it comes into
        // the match already owning Slam's prerequisite, and the match's one pick for it buys Slam.
        Table.CreatureNumber(match, 1).BuyTier(Arena.Resources.GetTier(Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(CreatureId.From(1), Arena.SlamPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.ChooseStandard(match);
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), Arena.Slam)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(2), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(3), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(4), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(1), Arena.Slam), [CreatureId.From(3), CreatureId.From(4)])).IsSuccess.ShouldBeTrue();

        Table.HitFirstLivingEnemy(match);

        var steps = match.DomainEvents.OfType<CombatActionResolved>().ToList();
        steps[0].Resolution.Outcomes.Count.ShouldBe(4);
        steps.Skip(2).Select(step => step.Resolution.FizzleReason).ShouldBe([CombatErrors.ActorStunned, CombatErrors.ActorStunned]);
        Table.CreatureNumber(match, 3).IsStunned.ShouldBeTrue();
        Table.CreatureNumber(match, 1).Energy.ShouldBe(Energy.Of(2));

        Table.PassEvolution(match);
        match.SubmitSpeedChoice(PlayerSlot.Player2, new SpeedChoice(CreatureId.From(3), Speed.Standard)).Error.ShouldBe(PlanningErrors.CreatureStunned);
        Table.ChooseStandard(match);

        match.CurrentRound.ShouldNotBeNull().SubPhase.ShouldBe(RoundSubPhase.IntentSelection);
        match.CurrentRound.Timeline.Slots.Select(slot => slot.Creature).ShouldBe([CreatureId.From(1), CreatureId.From(2)]);

        Table.DeclareStrikes(match);
        Table.HitFirstLivingEnemy(match);

        Table.CreatureNumber(match, 3).IsStunned.ShouldBeFalse();
        Table.CreatureNumber(match, 3).Health.ShouldBe(Health.Of(9));

        // ADR 0072: the cleanup that ended both stuns left both creatures immune through the next round.
        var immune = match.DomainEvents.OfType<StunImmunityGained>().ShouldHaveSingleItem();
        immune.Creatures.ShouldBe([CreatureId.From(3), CreatureId.From(4)], ignoreOrder: true);
        Table.CreatureNumber(match, 3).IsStunImmune.ShouldBeTrue();
        Table.CreatureNumber(match, 4).IsStunImmune.ShouldBeTrue();
        Table.CreatureNumber(match, 1).IsStunImmune.ShouldBeFalse();
    }

    [Fact]
    public void A_symmetric_round_at_the_cap_is_a_draw()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));

        var steps = Table.PlayRound(match);

        steps.Count.ShouldBe(4);
        match.State.ShouldBe(MatchState.Ended);
        var outcome = match.Outcome.ShouldNotBeNull();
        outcome.ShouldBe(new MatchOutcome(null, MatchEndReason.RoundCap));
        outcome.IsDraw.ShouldBeTrue();
    }

    [Fact]
    public void Critical_hits_come_from_the_random_source()
    {
        var match = Table.Started(random: new FixedRandom(0.0));
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        var round = match.CurrentRound.ShouldNotBeNull();
        var slot = round.NextSlot.ShouldNotBeNull();

        match.SubmitAction(slot.Owner, CombatAction.Bind(round.IntentOf(slot.Creature).ShouldNotBeNull(), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();

        var step = match.DomainEvents.OfType<CombatActionResolved>().Single();
        step.Resolution.IsCritical.ShouldBeTrue();
        step.Resolution.Outcomes.ShouldBe([new DamageOutcome(CreatureId.From(3), 6, true)]);
        Table.CreatureNumber(match, 3).Health.ShouldBe(Health.Of(14));
    }
}
