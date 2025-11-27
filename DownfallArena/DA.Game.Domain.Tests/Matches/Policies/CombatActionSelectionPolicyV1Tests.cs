using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
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

public class CombatActionSelectionPolicyV1Tests
{
    // --------------------------
    // INVARIANT FAILURES (Ixxx)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenMatchNotStarted_I101(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var ctx = baseCtx with { State = MatchState.WaitingForPlayers };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I101 - Match must be started to submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenPhaseIsNotCombat_I102(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var ctx = baseCtx with { Phase = RoundPhase.Planning };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeTrue();
        result.Error.Should().Be("I102 - Round phase must be Combat to submit a combat action.");
    }

    // --------------------------
    // DOMAIN FAILURES (Dxxx)
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorDead_D201(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat
        };

        var deadActor = CloneUtility.CloneSnapshot(normalized.Actor, health: Health.Of(0), isStunned: false);

        var newCreatures = normalized.Creatures
            .Select(c => c.CharacterId == normalized.ActorId ? deadActor : c)
            .ToList();

        var ctx = normalized with { Creatures = newCreatures };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D201 - Dead creature cannot submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActorStunned_D202(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat
        };

        var stunnedActor = CloneUtility.CloneSnapshot(
            normalized.Actor,
            health: normalized.Actor.Health,
            isStunned: true);

        var newCreatures = normalized.Creatures
            .Select(c => c.CharacterId == normalized.ActorId ? stunnedActor : c)
            .ToList();

        var ctx = normalized with { Creatures = newCreatures };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D202 - Stunned creature cannot submit a combat action.");
    }

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenActionAlreadySubmitted_D203(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut,
        CombatActionChoice existingChoice)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat
        };

        var dict = new Dictionary<CreatureId, CombatActionChoice>
        {
            { normalized.ActorId, existingChoice }
        };

        var readOnly = new ReadOnlyDictionary<CreatureId, CombatActionChoice>(dict);

        var ctx = normalized with { CombatActionChoices = readOnly };

        var result = sut.EnsureActionIsValid(ctx);

        result.IsSuccess.Should().BeFalse();
        result.IsInvariant.Should().BeFalse();
        result.Error.Should().Be("D203 - This creature has already submitted a combat action for this round.");
    }

    // --------------------------
    // SUCCESS
    // --------------------------

    [Theory, MatchAutoData]
    public void EnsureActionIsValid_WhenAllRulesPass_Succeeds(
        CreaturePerspective baseCtx,
        CombatActionSelectionPolicyV1 sut)
    {
        var normalized = WithActorAliveAndNotStunned(baseCtx) with
        {
            State = MatchState.Started,
            Phase = RoundPhase.Combat,
            CombatActionChoices = null
        };

        var result = sut.EnsureActionIsValid(normalized);

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
}
