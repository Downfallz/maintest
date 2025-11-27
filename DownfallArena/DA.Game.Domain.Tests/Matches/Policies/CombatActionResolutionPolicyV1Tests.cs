using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Tests;
using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace DA.Game.Domain.Tests.Matches.Policies;

public class CombatActionResolutionPolicyV1Tests
{
    // --------------------------
    // INVARIANT FAILURES (Ixxx)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenMatchNotStarted_I001(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut)
    {
        var ctx = baseCtx with { State = MatchState.WaitingForPlayers };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I001 - Match must be started to resolve a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenPhaseIsNotCombat_I002(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut)
    {
        var ctx = baseCtx with { Phase = RoundPhase.Planning };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I002 - Round phase must be Combat to resolve a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenTimelineMissing_I003(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut)
    {
        var ctx = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = null
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I003 - Timeline is not initialized for this round.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenRoundAlreadyCompleted_I004(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut,
        CombatActionChoice submittedAction)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        // Timeline avec 1 slot pour l'acteur, puis MoveNext => IsComplete == true
        var slot = BuildActorSlot(normalized);
        var timeline = CombatTimeline
            .FromSlots(new[] { slot })
            .MoveNext();

        var choices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>
            {
                { normalized.ActorId, submittedAction }
            });

        var ctx = normalized with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = choices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I004 - Round is already completed; no further combat actions can be resolved.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenNotActorsTurn_I006(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut,
        CreatureId otherCreatureId,
        CombatActionChoice submittedAction)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        // Choisir un id différent de l'acteur
        var notActorId = otherCreatureId == normalized.ActorId
            ? normalized.Creatures.First(c => c.CharacterId != normalized.ActorId).CharacterId
            : otherCreatureId;

        var slot = new ActivationSlot(
            normalized.Actor.OwnerSlot,
            notActorId,
            default,
            normalized.Actor.Initiative);

        var timeline = CombatTimeline.FromSlots(new[] { slot });

        var choices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>
            {
                { normalized.ActorId, submittedAction }
            });

        var ctx = normalized with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = choices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I006 - It is not this creature's turn to resolve a combat action.");
    }

    // --------------------------
    // DOMAIN FAILURES (Dxxx)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorDead_D101(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut,
        CombatActionChoice submittedAction)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        var slot = BuildActorSlot(normalized);
        var timeline = CombatTimeline.FromSlots(new[] { slot });

        var deadActor = CloneUtility.CloneSnapshot(normalized.Actor, health: Health.Of(0));

        var newCreatures = normalized.Creatures
            .Select(c => c.CharacterId == normalized.ActorId ? deadActor : c)
            .ToList();

        var choices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>
            {
                { normalized.ActorId, submittedAction }
            });

        var ctx = normalized with
        {
            Creatures = newCreatures,
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = choices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D101 - Dead creature cannot resolve a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorStunned_D102(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut,
        CombatActionChoice submittedAction)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        var slot = BuildActorSlot(normalized);
        var timeline = CombatTimeline.FromSlots(new[] { slot });

        var stunnedActor = CloneUtility.CloneSnapshot(
            normalized.Actor,
            health: Health.Of(normalized.Actor.Health.Value <= 0 ? 1 : normalized.Actor.Health.Value),
            isStunned: true);

        var newCreatures = normalized.Creatures
            .Select(c => c.CharacterId == normalized.ActorId ? stunnedActor : c)
            .ToList();

        var choices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>
            {
                { normalized.ActorId, submittedAction }
            });

        var ctx = normalized with
        {
            Creatures = newCreatures,
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = choices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D102 - Stunned creature cannot resolve a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenNoSubmittedAction_D103(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        var slot = BuildActorSlot(normalized);
        var timeline = CombatTimeline.FromSlots(new[] { slot });

        var emptyChoices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>());

        var ctx = normalized with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = emptyChoices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D103 - No combat action was submitted for this creature.");
    }

    // --------------------------
    // SUCCESS
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenAllRulesPass_Succeeds(
        CreaturePerspective baseCtx,
        CombatActionResolutionPolicyV1 sut,
        CombatActionChoice submittedAction)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx);

        var slot = BuildActorSlot(normalized);
        var timeline = CombatTimeline.FromSlots(new[] { slot });

        var choices = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(
            new Dictionary<CreatureId, CombatActionChoice>
            {
                { normalized.ActorId, submittedAction }
            });

        var ctx = normalized with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            Timeline = timeline,
            CombatActionChoices = choices
        };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeTrue();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    // --------------------------
    // Helpers
    // --------------------------

    private static CreaturePerspective WithActorAliveAndNotStunned(CreaturePerspective ctx)
    {
        var fixedActor = CloneUtility.CloneSnapshot(
            ctx.Actor,
            health: Health.Of(ctx.Actor.Health.Value <= 0 ? 1 : ctx.Actor.Health.Value),
            isStunned: false);

        var newCreatures = ctx.Creatures
            .Select(c => c.CharacterId == ctx.ActorId ? fixedActor : c)
            .ToList();

        return ctx with { Creatures = newCreatures };
    }

    private static ActivationSlot BuildActorSlot(CreaturePerspective ctx)
    {
        return new ActivationSlot(
            ctx.Actor.OwnerSlot,
            ctx.ActorId,
            default,                  // SkillSpeed (peu importe ici)
            ctx.Actor.Initiative);
    }
}

