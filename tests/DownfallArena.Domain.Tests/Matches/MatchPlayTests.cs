using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
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

        // Each side focuses the first living enemy; Player1 acts first in every slot, so its second creature
        // finishes the duel with two health left.
        rounds.ShouldBe(11);
        match.State.ShouldBe(MatchState.Ended);
        var outcome = match.Outcome.ShouldNotBeNull();
        outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        match.TeamOf(PlayerSlot.Player2).IsDefeated.ShouldBeTrue();
        match.TeamOf(PlayerSlot.Player1).Find(CreatureId.From(2)).ShouldNotBeNull().Health.ShouldBe(Health.Of(2));
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(11);
        match.CurrentRound.IsFinalized.ShouldBeTrue();
        match.DomainEvents.OfType<MatchEnded>().Single().ShouldBe(new MatchEnded(match.Id, RoundId.From(11), outcome));
        match.DomainEvents.OfType<RoundStarted>().Count().ShouldBe(11);

        match.PassEvolution(PlayerSlot.Player1).Error.ShouldBe(MatchErrors.NotInProgress);
        match.Join(PlayerId.New(), Table.Roster(match)).Error.ShouldBe(MatchErrors.AlreadyStarted);
    }

    [Fact]
    public void Actions_of_creatures_that_died_earlier_in_the_round_fizzle()
    {
        var match = Table.Started();
        var steps = new List<CombatStep>();
        for (var round = 0; round < 4; round++)
        {
            steps = Table.PlayRound(match);
        }

        // Round 4: creature 1 kills creature 3, creature 2 loses its only target, creature 3 is dead, creature 4 kills creature 1.
        steps.Select(step => step.Resolution.FizzleReason).ShouldBe([null, CombatErrors.AllTargetsInvalid, CombatErrors.ActorDead, null]);
        match.TeamOf(PlayerSlot.Player1).Find(CreatureId.From(1)).ShouldNotBeNull().IsDead.ShouldBeTrue();
        match.TeamOf(PlayerSlot.Player2).Find(CreatureId.From(3)).ShouldNotBeNull().IsDead.ShouldBeTrue();
        match.State.ShouldBe(MatchState.InProgress);
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(5);
    }

    [Fact]
    public void The_round_cap_ends_the_match_on_total_health()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));

        // Bob's first creature learns Guard and guards instead of striking: Bob deals less and takes the same.
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(CreatureId.From(3), Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.ChooseStandard(match);
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(2), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(3), Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.SubmitIntent(PlayerSlot.Player2, new CombatIntent(CreatureId.From(4), Arena.Strike)).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(1), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player1, CombatAction.Bind(new CombatIntent(CreatureId.From(2), Arena.Strike), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(3), Arena.Guard), [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Bind(new CombatIntent(CreatureId.From(4), Arena.Strike), [CreatureId.From(1)])).IsSuccess.ShouldBeTrue();

        var steps = Table.ResolveAll(match);

        steps[3].MatchEnded.ShouldBeTrue();
        steps[3].RoundCompleted.ShouldBeTrue();
        match.State.ShouldBe(MatchState.Ended);
        match.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.RoundCap));
        match.TeamOf(PlayerSlot.Player1).TotalHealth.ShouldBe(37);
        match.TeamOf(PlayerSlot.Player2).TotalHealth.ShouldBe(34);
        match.TeamOf(PlayerSlot.Player2).Find(CreatureId.From(3)).ShouldNotBeNull().TotalDefense.ShouldBe(Defense.Of(2));
        match.DomainEvents.OfType<MatchEnded>().Single().LastRound.ShouldBe(RoundId.First);
    }

    [Fact]
    public void A_symmetric_round_at_the_cap_is_a_draw()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));

        var steps = Table.PlayRound(match);

        steps[3].MatchEnded.ShouldBeTrue();
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
        Table.HitFirstLivingEnemy(match);

        var step = match.ResolveNextAction().Value;

        step.Resolution.IsCritical.ShouldBeTrue();
        step.Resolution.Outcomes.ShouldBe([new DamageOutcome(CreatureId.From(3), 6, true)]);
        match.TeamOf(PlayerSlot.Player2).Find(CreatureId.From(3)).ShouldNotBeNull().Health.ShouldBe(Health.Of(14));
    }
}
