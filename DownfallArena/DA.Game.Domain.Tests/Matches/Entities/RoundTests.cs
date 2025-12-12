using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Messages;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DA.Game.Domain.Tests.Matches.Entities;


public sealed class RoundTests
{
    // ---------------------------------------------------------------------
    // Helper to build a simple 1-slot timeline
    // ---------------------------------------------------------------------
    private static CombatTimeline BuildTimeline(CreaturePerspective p, int initiative = 10)
    {
        var slot = new ActivationSlot(
            p.Actor.OwnerSlot,
            p.Actor.CharacterId,
            SkillSpeed.Standard,
            new Initiative(initiative));

        return CombatTimeline.FromSlots(new[] { slot });
    }

    // ---------------------------------------------------------------------
    // INTENT REVEAL FLOW
    // ---------------------------------------------------------------------

    [Theory, MatchAutoData]
    public void RevealIntent_WhenValid_ShouldReturnIntent(
        Round round,
        CreaturePerspective perspective,
        Spell spell)
    {
        // Arrange
        round.InitializeEvolutionPhase();
        round.InitializeSpeedPhase();

        var timeline = BuildTimeline(perspective);
        round.InitializeTurnOrderResolution(timeline);

        round.InitializeCombatPhase();

        var intent = CombatActionIntent.Create(perspective.Actor.CharacterId, spell);
        round.SubmitCombatIntent(perspective, intent);

        round.InitializeCombatReveal();

        // Act
        var result = round.SelectNextIntentToReveal();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(intent);
        round.RevealCursor.Index.Should().Be(1);
    }

    [Theory, MatchAutoData]
    public void RevealIntent_WhenWrongPhase_ShouldFailInvariant(Round round)
    {
        var result = round.SelectNextIntentToReveal();

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be(RoundErrorCodes.I301_INVALID_PHASE_FOR_REVEAL);
    }

    // ---------------------------------------------------------------------
    // COMBAT ACTION RESOLUTION FLOW
    // ---------------------------------------------------------------------

    [Theory, MatchAutoData]
    public void ResolveAction_WhenValid_ShouldReturnAction(
        Round round,
        CreaturePerspective perspective,
        Spell spell)
    {
        // Arrange
        round.InitializeEvolutionPhase();
        round.InitializeSpeedPhase();

        var timeline = BuildTimeline(perspective);
        round.InitializeTurnOrderResolution(timeline);

        round.InitializeCombatPhase();

        // Intent
        var intent = CombatActionIntent.Create(perspective.Actor.CharacterId, spell);
        round.SubmitCombatIntent(perspective, intent);

        round.InitializeCombatReveal();

        // Choice
        var choice = CombatActionChoice.FromIntentAndTargets(
            intent,
            new[] { perspective.Actor.CharacterId });

        round.SubmitCombatAction(perspective, choice);

        round.InitializeCombatResolution();

        // Act
        var result = round.SelectNextActionToResolve();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(choice);
    }

    [Theory, MatchAutoData]
    public void ResolveAction_WhenWrongPhase_ShouldFailInvariant(
        Round round)
    {
        var result = round.SelectNextActionToResolve();

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be(RoundErrorCodes.I302_INVALID_PHASE_FOR_RESOLUTION);
    }

    // ---------------------------------------------------------------------
    // CURSOR ADVANCEMENT + RESOLUTION COMPLETION
    // ---------------------------------------------------------------------

    [Theory, MatchAutoData]
    public void AdvanceCursor_WhenLastSlot_ShouldMarkCompletion(
        Round round,
        CreaturePerspective perspective,
        Spell spell)
    {
        // Arrange
        round.InitializeEvolutionPhase();
        round.InitializeSpeedPhase();

        var timeline = BuildTimeline(perspective);
        round.InitializeTurnOrderResolution(timeline);

        round.InitializeCombatPhase();

        var intent = CombatActionIntent.Create(perspective.Actor.CharacterId, spell);
        round.SubmitCombatIntent(perspective, intent);

        round.InitializeCombatReveal();

        var choice = CombatActionChoice.FromIntentAndTargets(
            intent,
            new[] { perspective.Actor.CharacterId });

        round.SubmitCombatAction(perspective, choice);
        round.InitializeCombatResolution();

        round.SelectNextActionToResolve(); // consume the only slot

        // Act
        var advance = round.AdvanceCombatResolutionCursor();

        // Assert
        advance.IsSuccess.Should().BeTrue();
        round.IsCombatResolutionCompleted.Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // END-OF-ROUND FLOW
    // ---------------------------------------------------------------------

    [Theory, MatchAutoData]
    public void EndOfRound_ShouldMoveToCleanupAndFinalize(Round round)
    {
        // Move through early phases
        round.InitializeEvolutionPhase();
        round.InitializeSpeedPhase();
        round.InitializeTurnOrderResolution(CombatTimeline.Empty);
        round.InitializeCombatPhase();
        round.InitializeCombatResolution();

        // Act 1 → move to cleanup
        var cleanup = round.InitializeEndOfRoundCleanup();

        cleanup.IsSuccess.Should().BeTrue();
        round.Phase.Should().Be(RoundPhase.EndOfRound);
        round.SubPhase.Should().Be(RoundSubPhase.End_Cleanup);

        // Act 2 → finalize
        var finalize = round.MarkEndOfRoundFinalized();

        finalize.IsSuccess.Should().BeTrue();
        round.SubPhase.Should().Be(RoundSubPhase.End_Finalization);
    }

    [Theory, MatchAutoData]
    public void FinalizeEndOfRound_WhenWrongPhase_ShouldFailInvariant(Round round)
    {
        var result = round.MarkEndOfRoundFinalized();

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be(RoundErrorCodes.I501_INVALID_PHASE_FOR_ROUND_FINALIZATION);
    }

    // ---------------------------------------------------------------------
    // HAPPY PATH INTEGRATION (MINIMAL ROUND)
    // ---------------------------------------------------------------------

    [Theory, MatchAutoData]
    public void FullRoundMinimal_HappyPath_ShouldCompleteCombat(
        Round round,
        CreaturePerspective perspective,
        Spell spell)
    {
        // Planning → evolution
        round.InitializeEvolutionPhase();
        round.SubmitEvolutionChoice(
            perspective,
            SpellUnlockChoice.Of(perspective.Actor.CharacterId, spell));

        // Planning → speed
        round.InitializeSpeedPhase();
        round.SubmitSpeedChoice(
            perspective,
            SpeedChoice.Of(perspective.Actor.CharacterId, SkillSpeed.Standard));

        // Turn order
        round.InitializeTurnOrderResolution(BuildTimeline(perspective));

        // Combat init
        round.InitializeCombatPhase();

        // Intent
        var intent = CombatActionIntent.Create(perspective.Actor.CharacterId, spell);
        round.SubmitCombatIntent(perspective, intent);

        round.InitializeCombatReveal();
        round.SelectNextIntentToReveal();

        // Action
        var choice = CombatActionChoice.FromIntentAndTargets(
            intent,
            new[] { perspective.Actor.CharacterId });

        round.SubmitCombatAction(perspective, choice);

        round.InitializeCombatResolution();
        var act = round.SelectNextActionToResolve();
        act.IsSuccess.Should().BeTrue();

        round.AdvanceCombatResolutionCursor();

        // END OF ROUND
        round.InitializeEndOfRoundCleanup().IsSuccess.Should().BeTrue();
        round.MarkEndOfRoundFinalized().IsSuccess.Should().BeTrue();

        // Assert final state
        round.Phase.Should().Be(RoundPhase.EndOfRound);
        round.SubPhase.Should().Be(RoundSubPhase.End_Finalization);
        round.IsCombatResolutionCompleted.Should().BeTrue();
    }
}

