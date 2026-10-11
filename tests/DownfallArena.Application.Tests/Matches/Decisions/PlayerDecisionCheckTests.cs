using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Decisions;

public sealed class PlayerDecisionCheckTests
{
    private static readonly CreatureId Mine = CreatureId.From(1);
    private static readonly CreatureId Theirs = CreatureId.From(3);

    [Fact]
    public void A_decision_of_another_kind_than_the_one_pending_is_refused()
    {
        var pending = new PlayerOptions { Kind = PlayerOptionsKind.Speed, Speed = new SpeedOptions([Mine]) };

        var refused = PlayerDecisionCheck.Validate(pending, PlayerDecision.DeclareIntent(Mine, TestContent.Strike));

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(DecisionErrors.NotPending);
    }

    [Fact]
    public void Nothing_is_accepted_while_the_seat_is_waiting_or_the_match_has_ended()
    {
        foreach (var kind in new[] { PlayerOptionsKind.Waiting, PlayerOptionsKind.Ended })
        {
            var options = new PlayerOptions { Kind = kind };

            PlayerDecisionCheck.Validate(options, PlayerDecision.Pass).Error.ShouldBe(DecisionErrors.NotPending);
        }
    }

    [Fact]
    public void An_unlock_the_options_offer_is_accepted_and_one_for_another_creature_is_not()
    {
        var options = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Evolution,
            Evolution = new EvolutionOptions(2, [new EvolutionOption(Mine, [TestContent.GuardPack])]),
        };

        PlayerDecisionCheck.Validate(options, PlayerDecision.Buy(Mine, TestContent.GuardPack)).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.Buy(Theirs, TestContent.GuardPack)).Error.ShouldBe(DecisionErrors.CreatureNotOffered);
        PlayerDecisionCheck.Validate(options, PlayerDecision.Buy(Mine, TestContent.SlamPack)).Error.ShouldBe(DecisionErrors.TierNotOffered);
    }

    [Fact]
    public void Passing_is_accepted_whenever_evolution_is_pending()
    {
        var nothingLeftToUnlock = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Evolution,
            Evolution = new EvolutionOptions(1, []),
        };

        PlayerDecisionCheck.Validate(nothingLeftToUnlock, PlayerDecision.Pass).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_speed_choice_is_accepted_only_for_a_creature_that_still_needs_one()
    {
        var options = new PlayerOptions { Kind = PlayerOptionsKind.Speed, Speed = new SpeedOptions([Mine]) };

        PlayerDecisionCheck.Validate(options, PlayerDecision.ChooseSpeed(Mine, Speed.Quick)).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.ChooseSpeed(Theirs, Speed.Standard)).Error.ShouldBe(DecisionErrors.CreatureNotOffered);
    }

    /// <summary>
    /// A host parses a speed off a request, and `Enum.TryParse` takes "7" as happily as "Quick". An undefined
    /// value passes every gate after this one and reaches the timeline builder, where its number becomes a
    /// priority band nobody wrote.
    /// </summary>
    [Fact]
    public void A_speed_that_is_neither_quick_nor_standard_is_refused()
    {
        var options = new PlayerOptions { Kind = PlayerOptionsKind.Speed, Speed = new SpeedOptions([Mine]) };

        var refused = PlayerDecisionCheck.Validate(options, PlayerDecision.ChooseSpeed(Mine, (Speed)7));

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(DecisionErrors.NoSpeedChosen);
    }

    [Fact]
    public void An_intent_is_accepted_only_for_a_spell_the_creature_can_afford()
    {
        var options = new PlayerOptions
        {
            Kind = PlayerOptionsKind.Intent,
            Intent = new IntentOptions([new IntentOption(Mine, [TestContent.Strike])]),
        };

        PlayerDecisionCheck.Validate(options, PlayerDecision.DeclareIntent(Mine, TestContent.Strike)).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.DeclareIntent(Mine, TestContent.Slam)).Error.ShouldBe(DecisionErrors.SpellNotOffered);
        PlayerDecisionCheck.Validate(options, PlayerDecision.DeclareIntent(Theirs, TestContent.Strike)).Error.ShouldBe(DecisionErrors.CreatureNotOffered);
    }

    [Fact]
    public void Targets_must_be_candidates_the_options_listed_without_repeating_one()
    {
        var options = Targeting(new LegalTargets(1, 2, [Theirs, CreatureId.From(4)]));

        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([Theirs])).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([Mine])).Error.ShouldBe(DecisionErrors.TargetNotOffered);
        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([Theirs, Theirs])).Error.ShouldBe(DecisionErrors.DuplicateTarget);
    }

    [Fact]
    public void A_binding_must_hold_as_many_targets_as_the_spell_asks_and_no_more()
    {
        var options = Targeting(new LegalTargets(1, 2, [Theirs, CreatureId.From(4), CreatureId.From(5)]));

        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([])).Error.ShouldBe(DecisionErrors.TooFewTargets);
        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([Theirs, CreatureId.From(4), CreatureId.From(5)]))
            .Error.ShouldBe(DecisionErrors.TooManyTargets);
    }

    /// <summary>
    /// A spell with nothing left to hit is revealed with no targets and fizzles, so binding none is the action,
    /// not a refusal. The seat that taps it must be told so, or a player is stuck on a screen with no legal move.
    /// </summary>
    [Fact]
    public void Binding_no_target_is_accepted_when_the_spell_has_no_legal_one()
    {
        var options = Targeting(new LegalTargets(1, 1, []));

        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([])).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.BindTargets([Theirs])).Error.ShouldBe(DecisionErrors.NoLegalTarget);
    }

    /// <summary>
    /// The check is only worth anything if it agrees with the projection a seat is actually served. This walks
    /// a real match and submits, at each sub-phase, exactly what the options offered.
    /// </summary>
    [Fact]
    public void Everything_the_projection_offers_on_a_real_match_is_accepted()
    {
        var match = new MatchStore().Started();

        var evolution = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        var offered = evolution.Evolution.ShouldNotBeNull().Creatures[0];
        PlayerDecisionCheck.Validate(evolution, PlayerDecision.Buy(offered.Creature, offered.AvailableTiers[0])).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(evolution, PlayerDecision.Pass).IsSuccess.ShouldBeTrue();
        MatchStore.PassEvolution(match);

        var speed = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        PlayerDecisionCheck.Validate(speed, PlayerDecision.ChooseSpeed(speed.Speed.ShouldNotBeNull().Missing[0], Speed.Quick)).IsSuccess.ShouldBeTrue();
        MatchStore.ChooseStandard(match);

        var intent = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        var castable = intent.Intent.ShouldNotBeNull().Creatures[0];
        PlayerDecisionCheck.Validate(intent, PlayerDecision.DeclareIntent(castable.Creature, castable.CastableSpells[0])).IsSuccess.ShouldBeTrue();
        MatchStore.DeclareStrikes(match);

        var target = PlayerOptionsProjection.Build(match, PlayerSlot.Player1, TestContent.Resources);
        var legal = target.Target.ShouldNotBeNull().LegalTargets;
        PlayerDecisionCheck.Validate(target, PlayerDecision.BindTargets([legal.Candidates[0]])).IsSuccess.ShouldBeTrue();
    }

    /// <summary>ADR 0110: the creature at its slot chooses a spell and its targets in one decision.</summary>
    [Fact]
    public void An_action_carries_its_creature_its_spell_and_a_copy_of_its_targets()
    {
        List<CreatureId> targets = [Theirs];

        var decision = PlayerDecision.Act(Mine, TestContent.Strike, targets);
        targets.Add(CreatureId.From(4));

        decision.Kind.ShouldBe(PlayerOptionsKind.Action);
        decision.Creature.ShouldBe(Mine);
        decision.Spell.ShouldBe(TestContent.Strike);
        decision.Targets.ShouldBe([Theirs]);
    }

    [Fact]
    public void An_action_without_a_target_list_is_rejected() =>
        Should.Throw<ArgumentNullException>(() => PlayerDecision.Act(Mine, TestContent.Strike, null!));

    [Fact]
    public void An_action_the_options_offer_is_accepted()
    {
        var options = Acting();

        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Strike, [Theirs])).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Slam, [Theirs, CreatureId.From(4)])).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_action_for_a_creature_other_than_the_one_at_its_slot_is_refused()
    {
        var refused = PlayerDecisionCheck.Validate(Acting(), PlayerDecision.Act(Theirs, TestContent.Strike, [Theirs]));

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(DecisionErrors.CreatureNotOffered);
    }

    [Fact]
    public void An_action_with_a_spell_the_options_do_not_offer_is_refused()
    {
        var refused = PlayerDecisionCheck.Validate(Acting(), PlayerDecision.Act(Mine, TestContent.Guard, [Mine]));

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(DecisionErrors.SpellNotOffered);
    }

    /// <summary>The targets of an action are read against the chosen spell's, exactly as a binding's are.</summary>
    [Fact]
    public void An_action_s_targets_are_checked_against_its_spell_as_a_binding_is()
    {
        var options = Acting();

        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Strike, [])).Error.ShouldBe(DecisionErrors.TooFewTargets);
        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Strike, [Theirs, CreatureId.From(4)])).Error.ShouldBe(DecisionErrors.TooManyTargets);
        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Slam, [Theirs, Theirs])).Error.ShouldBe(DecisionErrors.DuplicateTarget);
        PlayerDecisionCheck.Validate(options, PlayerDecision.Act(Mine, TestContent.Strike, [Mine])).Error.ShouldBe(DecisionErrors.TargetNotOffered);
    }

    [Fact]
    public void A_target_binding_is_not_what_an_action_slot_is_waiting_for()
    {
        PlayerDecisionCheck.Validate(Acting(), PlayerDecision.BindTargets([Theirs])).Error.ShouldBe(DecisionErrors.NotPending);
        PlayerDecisionCheck.Validate(Targeting(new LegalTargets(1, 1, [Theirs])), PlayerDecision.Act(Mine, TestContent.Strike, [Theirs])).Error.ShouldBe(DecisionErrors.NotPending);
    }

    /// <summary>
    /// ADR 0110. The check agrees with the projection a seat is served and with the match itself: on a real
    /// match that declares no intent, every slot is answered with the first spell and the first target the
    /// options offer, and the match takes each one.
    /// </summary>
    [Fact]
    public void Everything_the_projection_offers_at_activation_on_a_real_match_is_accepted()
    {
        var match = new MatchStore().Started(MatchStore.TwoOnTwo().WithCombatDeclaration(CombatDeclaration.AtActivation));
        MatchStore.PassEvolution(match);
        MatchStore.ChooseStandard(match);

        var next = match.CurrentRound.ShouldNotBeNull().NextSlot.ShouldNotBeNull();
        var options = PlayerOptionsProjection.Build(match, next.Owner, TestContent.Resources);
        var offered = options.Action.ShouldNotBeNull().Spells[0];
        var decision = PlayerDecision.Act(offered.Actor, offered.Spell, [offered.LegalTargets.Candidates[0]]);

        PlayerDecisionCheck.Validate(options, decision).IsSuccess.ShouldBeTrue();
        match.SubmitAction(next.Owner, CombatAction.Bind(new CombatIntent(offered.Actor, offered.Spell), decision.Targets)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Creature 1 at its slot, able to cast Slam on up to two enemies or Strike on one.</summary>
    private static PlayerOptions Acting() => new()
    {
        Kind = PlayerOptionsKind.Action,
        Action = new ActionOptions(
            Mine,
            [
                new TargetOptions(Mine, TestContent.Slam, new LegalTargets(1, 2, [Theirs, CreatureId.From(4)])),
                new TargetOptions(Mine, TestContent.Strike, new LegalTargets(1, 1, [Theirs, CreatureId.From(4)])),
            ]),
    };

    private static PlayerOptions Targeting(LegalTargets legal) => new()
    {
        Kind = PlayerOptionsKind.Target,
        Target = new TargetOptions(Mine, TestContent.Strike, legal),
    };

    [Fact]
    public void A_tie_order_is_accepted_only_when_it_names_every_offered_creature_once()
    {
        var pending = new PlayerOptions { Kind = PlayerOptionsKind.TieOrder, TieOrder = new TieOrderOptions([[Mine, CreatureId.From(2)]]) };

        PlayerDecisionCheck.Validate(pending, PlayerDecision.OrderTies([CreatureId.From(2), Mine])).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(pending, PlayerDecision.OrderTies([Mine])).Error.ShouldBe(DecisionErrors.TieOrderNotOffered);
        PlayerDecisionCheck.Validate(pending, PlayerDecision.OrderTies([Mine, Mine])).Error.ShouldBe(DecisionErrors.TieOrderNotOffered);
        PlayerDecisionCheck.Validate(pending, PlayerDecision.OrderTies([Mine, Theirs])).Error.ShouldBe(DecisionErrors.TieOrderNotOffered);
    }

    [Fact]
    public void Two_tie_orders_naming_the_same_creatures_in_another_sequence_are_different_decisions()
    {
        PlayerDecision.OrderTies([Mine, Theirs]).ShouldNotBe(PlayerDecision.OrderTies([Theirs, Mine]));
        PlayerDecision.OrderTies([Mine, Theirs]).ShouldBe(PlayerDecision.OrderTies([Mine, Theirs]));
    }
}
