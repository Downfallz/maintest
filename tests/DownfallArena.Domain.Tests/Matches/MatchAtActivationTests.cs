using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Planning;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches;

/// <summary>
/// A rule set that declares no intent: each creature's owner chooses the spell with its targets when the slot
/// comes up (ADR 0110). The resolution is the one a revealed intent gets.
/// </summary>
public sealed class MatchAtActivationTests
{
    [Fact]
    public void Combat_opens_on_the_first_slot_without_asking_for_intents()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        Table.PassEvolution(match);

        Table.ChooseStandard(match);

        var round = match.CurrentRound.ShouldNotBeNull();
        round.SubPhase.ShouldBe(RoundSubPhase.Activation);
        round.NextSlot.ShouldNotBeNull().Creature.ShouldBe(CreatureId.From(1));
        match.DomainEvents.OfType<SubPhaseEntered>().Select(entered => entered.SubPhase)
            .ShouldContain(RoundSubPhase.IntentSelection, "the sub-phase is passed through, as a round with no tie passes Tie order");
        match.SubmitIntent(PlayerSlot.Player1, new CombatIntent(CreatureId.From(1), Arena.Strike)).Error.ShouldBe(RoundErrors.IntentsNotOpen);
    }

    [Fact]
    public void A_spell_chosen_with_its_targets_at_the_slot_resolves_on_confirmation()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        var action = CombatAction.Choose(CreatureId.From(1), Arena.Strike, [CreatureId.From(3)]);

        match.SubmitAction(PlayerSlot.Player1, action).IsSuccess.ShouldBeTrue();

        match.DomainEvents.OfType<IntentSubmitted>().ShouldBeEmpty();
        match.DomainEvents.OfType<ActionRevealed>().ShouldHaveSingleItem().Action.ShouldBe(action);
        var resolved = match.DomainEvents.OfType<CombatActionResolved>().ShouldHaveSingleItem();
        resolved.Resolution.FizzleReason.ShouldBeNull();
        Table.CreatureNumber(match, 3).Health.ShouldBe(Health.Of(17));
        match.CurrentRound.ShouldNotBeNull().ActivationCursor.Index.ShouldBe(1);
    }

    [Fact]
    public void Only_the_owner_of_the_creature_at_the_slot_chooses_its_action()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        Table.PassEvolution(match);
        Table.ChooseStandard(match);

        match.SubmitAction(PlayerSlot.Player2, CombatAction.Choose(CreatureId.From(1), Arena.Strike, [CreatureId.From(3)])).Error.ShouldBe(CombatErrors.NotYourCreature);
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Choose(CreatureId.From(3), Arena.Strike, [CreatureId.From(1)])).Error.ShouldBe(RoundErrors.NotThisCreaturesTurn);
    }

    /// <summary>
    /// Nothing is declared before the slot, so nothing is declared that cannot be paid: the price is checked when
    /// the spell is chosen, and the owner can choose another (ADR 0110; ADR 0107 holds for a hidden intent only).
    /// </summary>
    [Fact]
    public void A_spell_the_creature_cannot_pay_for_at_its_slot_is_refused_and_another_can_be_chosen()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(CreatureId.From(3), Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 3).LoseEnergy(99);

        Table.ChooseStandard(match);

        match.CurrentRound.ShouldNotBeNull().NextSlot.ShouldNotBeNull().Creature.ShouldBe(CreatureId.From(3), "Guard's initiative puts it first");
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Choose(CreatureId.From(3), Arena.Guard, [CreatureId.From(3)])).Error.ShouldBe(CombatErrors.NotEnoughEnergy);
        match.SubmitAction(PlayerSlot.Player2, CombatAction.Choose(CreatureId.From(3), Arena.Strike, [CreatureId.From(1)])).IsSuccess.ShouldBeTrue();
        match.DomainEvents.OfType<CombatActionResolved>().ShouldHaveSingleItem().Resolution.FizzleReason.ShouldBeNull();
    }

    /// <summary>Energy that arrives before the slot pays for the spell chosen at it, with no plan declared first.</summary>
    [Fact]
    public void Energy_given_before_the_slot_pays_for_the_spell_chosen_at_it()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(CreatureId.From(3), Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 3).LoseEnergy(99);
        Table.ChooseStandard(match);

        Table.CreatureNumber(match, 3).GainEnergy(1);

        match.SubmitAction(PlayerSlot.Player2, CombatAction.Choose(CreatureId.From(3), Arena.Guard, [CreatureId.From(3)])).IsSuccess.ShouldBeTrue();
        Table.CreatureNumber(match, 3).Energy.ShouldBe(Energy.Of(0));
    }

    /// <summary>
    /// A creature dead before its slot has no intent to reveal: its slot is skipped, its owner is not asked, and
    /// nothing resolves for it (ADR 0110).
    /// </summary>
    [Fact]
    public void A_creature_dead_before_its_slot_is_skipped_without_its_owner_being_asked()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.CreatureNumber(match, 3).TakeDamage(99);
        var seen = match.DomainEvents.Count;

        var resolved = Table.StrikeFirstLivingEnemy(match);

        resolved.Select(step => step.Resolution.Action.Actor).ShouldBe([CreatureId.From(1), CreatureId.From(2), CreatureId.From(4)]);
        resolved.ShouldAllBe(step => step.Resolution.FizzleReason == null);
        match.DomainEvents.Skip(seen).OfType<SlotSkipped>().ShouldHaveSingleItem()
            .ShouldBe(new SlotSkipped(match.Id, RoundId.From(1), CreatureId.From(3), CombatErrors.ActorDead));
        match.DomainEvents.Skip(seen).OfType<ActionRevealed>().Select(revealed => revealed.Action.Actor).ShouldNotContain(CreatureId.From(3));
        match.CurrentRound.ShouldNotBeNull().Number.ShouldBe(2);
    }

    /// <summary>
    /// When every creature casts what it would have declared, the two rules play the same match: what moves is
    /// when the spell is chosen, not how it resolves.
    /// </summary>
    [Fact]
    public void A_scripted_match_plays_to_the_same_elimination_as_with_hidden_intents()
    {
        var match = Table.Started(Table.TwoOnTwoAtActivation());
        var rounds = 0;

        while (match.State == MatchState.InProgress)
        {
            Table.PlayRoundAtActivation(match);
            rounds++;
        }

        rounds.ShouldBe(10);
        match.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        Table.CreatureNumber(match, 2).Health.ShouldBe(Health.Of(5));
        match.DomainEvents.OfType<IntentSubmitted>().ShouldBeEmpty();
        match.DomainEvents.OfType<SlotSkipped>().Select(skipped => skipped.Reason).ShouldAllBe(reason => reason == CombatErrors.ActorDead);
    }
}
