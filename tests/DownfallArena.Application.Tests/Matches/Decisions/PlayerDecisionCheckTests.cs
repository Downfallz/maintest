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
        foreach (var kind in new[] { PlayerOptionsKind.Waiting, PlayerOptionsKind.Resolution, PlayerOptionsKind.Ended })
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
            Evolution = new EvolutionOptions(2, [new EvolutionOption(Mine, [TestContent.Guard])]),
        };

        PlayerDecisionCheck.Validate(options, PlayerDecision.Unlock(Mine, TestContent.Guard)).IsSuccess.ShouldBeTrue();
        PlayerDecisionCheck.Validate(options, PlayerDecision.Unlock(Theirs, TestContent.Guard)).Error.ShouldBe(DecisionErrors.CreatureNotOffered);
        PlayerDecisionCheck.Validate(options, PlayerDecision.Unlock(Mine, TestContent.Slam)).Error.ShouldBe(DecisionErrors.SpellNotOffered);
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
        PlayerDecisionCheck.Validate(evolution, PlayerDecision.Unlock(offered.Creature, offered.UnlockableSpells[0])).IsSuccess.ShouldBeTrue();
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

    private static PlayerOptions Targeting(LegalTargets legal) => new()
    {
        Kind = PlayerOptionsKind.Target,
        Target = new TargetOptions(Mine, TestContent.Strike, legal),
    };
}
